using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;
using BeatSaberExtensions.Utility.Http.BeatLeader;
using BeatSaberExtensions.Utility.Http.BeatSaver;
using BeatSaberExtensions.Utility.LazyEvaluation;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Enums;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility;

public class BeatSaberService(IInlineInvokeProxy cph) : IDisposable
{
    private const string RaidRequestorGroupName = "Raid Requestors";
    private const string GlobalVarPrefix = "BeatSaberExtensions";

    private static readonly object _beatLeaderIdLock = new object();
    private static readonly Regex _beatmapIdPattern = new Regex(
        @"^(?<Id>[a-z0-9]{1,6})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture
    );
    private static readonly Cached<string> _beatSaberProcessPath = new Cached<string>(
        GetBeatSaberProcessPath,
        TimeSpan.FromSeconds(30),
        (value) => value is null
    );

    private Cached<bool> _queueState;
    private Cached<string> _beatSaberRoot;
    private bool _waitingForBeatLeaderId;

    public bool IsConfigured => !string.IsNullOrEmpty(GetBeatSaberRoot());
    public BeatSaverClient BeatSaverClient = new BeatSaverClient();
    public BeatLeaderClient BeatLeaderClient = new BeatLeaderClient();
    public bool QueueState => GetQueueState();
    public DatabaseJson DatabaseJson => GetDatabaseJson();
    public ReadOnlyCollection<QueueItem> Queue => DatabaseJson.Queue;
    public string BeatLeaderId
    {
        get => GetBeatLeaderId();
        set => cph.SetGlobalVar(GetGlobalVarName(), value);
    }

    public void SetBeatLeaderIdGlobal(string beatLeaderId) => BeatLeaderId = beatLeaderId;

    public bool UserIsInRaidRequestors(BaseUserInfo user) =>
        user is { UserId: { } userId }
        && cph.UserIdInGroup(userId, Platform.Twitch, RaidRequestorGroupName);

    public bool EnsureRaidRequestorsMembershipForUser(BaseUserInfo user, bool shouldBelongToGroup)
    {
        var logMessage = string.Format(
            "{0} raider {1} to Raid Requestors group.",
            shouldBelongToGroup ? "Adding" : "Removing",
            user.GetFormattedDisplayName()
        );

        Logger.Log(logMessage);

        return cph.EnsureGroupMembershipForUser(user, RaidRequestorGroupName, shouldBelongToGroup);
    }

    public string GetBeatmapId(string input) =>
        _beatmapIdPattern.Match((input ?? string.Empty).ToLowerInvariant()).Groups["Id"]
            is { Success: true, Value: { } value }
            ? value
            : null;

    public bool ValidateBeatmapId(string id)
    {
        if (Remap(GetBeatmapId(id)) is not { } bsrId || string.IsNullOrEmpty(bsrId))
        {
            return LogInvalidReasonAndReturn(
                "Failed to validate beatmap because the provided id was null or empty."
            );
        }

        if (DatabaseJson.Blacklist.Contains(bsrId))
        {
            return LogInvalidReasonAndReturn(
                $"Failed to validate beatmap id \"{bsrId}\" because it is on the blacklist."
            );
        }

        if (DatabaseJson.History.Contains(bsrId))
        {
            return LogInvalidReasonAndReturn(
                $"Failed to validate beatmap id \"{bsrId}\" because it was played recently."
            );
        }

        if (BeatSaverClient.GetBeatmap(bsrId) is not { } beatmap)
        {
            return LogInvalidReasonAndReturn(
                $"Failed to validate beatmap id \"{bsrId}\" because the beatmap could not be fetched from BeatSaver."
            );
        }

        if (
            beatmap is { Metadata.LevelAuthorName: { } author }
            && DatabaseJson.BannedMappers.Contains(author)
        )
        {
            return LogInvalidReasonAndReturn(
                $"Failed to validate beatmap id \"{bsrId}\" because the mapper ({author}) is on the banned mappers list."
            );
        }

        return true;
    }

    public void Dispose()
    {
        BeatSaverClient?.Dispose();
        BeatLeaderClient?.Dispose();
    }

    private bool GetQueueState() =>
        (
            _queueState ??= new Cached<bool>(
                () =>
                    JObject
                        .Parse(File.ReadAllText(GetBeatSaberPlusFilePath("Config.Json")))[
                            "QueueOpen"
                        ]
                        ?.Value<bool>() ?? false,
                TimeSpan.FromSeconds(5)
            )
        ).Value;

    private string Remap(string id)
    {
        if (id is null)
        {
            return id;
        }

        if (DatabaseJson.Remaps.TryGetValue(id, out var remap))
        {
            return remap;
        }

        return id;
    }

    private bool TryWaitForBeatLeaderId(
        out string beatLeaderId,
        Func<bool> condition = null,
        int? timeoutMs = null
    ) =>
        cph.TryWaitForCondition(
            valueFactory: () =>
                (condition?.Invoke() ?? true) && TryGetBeatLeaderIdGlobal(out var id) ? id : null,
            predicate: id => id is not null,
            result: out beatLeaderId,
            timeoutMs: timeoutMs ?? 15000
        );

    private string GetBeatLeaderId()
    {
        if (TryGetBeatLeaderIdGlobal(out var beatLeaderId))
        {
            Logger.Log($"Retrieved BeatLeaderId: {beatLeaderId}");

            return beatLeaderId;
        }

        if (_waitingForBeatLeaderId)
        {
            Logger.LogWarn(
                "Another thread is waiting for the BeatLeaderId. Waiting for it to complete..."
            );

            return TryWaitForBeatLeaderId(out beatLeaderId, () => !_waitingForBeatLeaderId)
                ? beatLeaderId
                : null;
        }

        lock (_beatLeaderIdLock)
        {
            // Check again inside lock to avoid race conditions
            if (TryGetBeatLeaderIdGlobal(out beatLeaderId))
            {
                Logger.Log($"Retrieved BeatLeaderId inside lock: {beatLeaderId}");

                return beatLeaderId;
            }

            try
            {
                _waitingForBeatLeaderId = true;

                Logger.Log("Sending !bsprofile command to retrieve BeatLeaderId");

                cph.SendMessage("!bsprofile");

                if (TryWaitForBeatLeaderId(out beatLeaderId))
                {
                    Logger.Log($"Successfully retrieved BeatLeaderId: {beatLeaderId}");

                    return beatLeaderId;
                }

                Logger.LogWarn("Failed to retrieve BeatLeaderId within 15 seconds");
            }
            catch (Exception ex)
            {
                Logger.HandleException(ex);
            }
            finally
            {
                _waitingForBeatLeaderId = false;
            }

            return null;
        }
    }

    private bool TryGetBeatLeaderIdGlobal(out string beatLeaderId)
    {
        beatLeaderId = cph.GetGlobalVar<string>(GetGlobalVarName(nameof(BeatLeaderId)));

        return beatLeaderId is not null;
    }

    private string GetBeatSaberPlusFilePath(string fileName)
    {
        if (!IsConfigured)
        {
            Logger.LogError(
                $"Cannot fetch Beat Saber Plus file: \"{fileName}\" because the BeatSaberRoot global var is not configured."
            );

            return null;
        }

        var bsPlusFilePath = Path.Combine(
            GetBeatSaberRoot(),
            "UserData",
            "BeatSaberPlus",
            "ChatRequest",
            fileName
        );

        return bsPlusFilePath;
    }

    private DatabaseJson GetDatabaseJson()
    {
        if (GetBeatSaberPlusFilePath("Database.json") is not { } path || string.IsNullOrEmpty(path))
        {
            throw new JsonSerializationException("Failed to deserialize Database.json.");
        }

        var json = File.ReadAllText(path);
        var internalData = JObject.Parse(json).ToObject<DatabaseJsonInternal>();
        var beatmaps = BeatSaverClient.GetBeatmaps(internalData.Queue.Select(item => item.Id));

        for (var i = 0; i < internalData.Queue.Count; i++)
        {
            var item = internalData.Queue[i];

            item.Position = i + 1;
            item.User = cph.GetUser<BaseUserInfo>(item.UserLogin);

            if (beatmaps.TryGetValue(internalData.Queue[i].Id, out var beatmap))
            {
                item.Beatmap = beatmap;
            }
        }

        var queue = internalData.Queue;
        var history = internalData
            .History.Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blacklist = internalData
            .Blacklist.Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bannedUsers = internalData.BannedUsers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bannedMappers = internalData.BannedMappers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remaps = internalData
            .Remaps.GroupBy(remap => remap.From)
            .ToDictionary(
                group => group.Key,
                group => group.First().To,
                StringComparer.OrdinalIgnoreCase
            );

        return new DatabaseJson
        {
            Queue = queue,
            History = history,
            Blacklist = blacklist,
            BannedUsers = bannedUsers,
            BannedMappers = bannedMappers,
            Remaps = remaps,
        };
    }

    private string GetBeatSaberRoot() =>
        (
            _beatSaberRoot ??= new Cached<string>(
                () =>
                {
                    if (
                        GetBeatSaberRootGlobal() is var globalValue
                        && _beatSaberProcessPath is { Value: { } processValue }
                        && (globalValue is null || globalValue != processValue)
                    )
                    {
                        cph.SetGlobalVar($"{GlobalVarPrefix}.BeatSaberRoot", processValue);

                        return processValue;
                    }

                    return globalValue;
                },
                TimeSpan.FromMinutes(5)
            )
        ).Value;

    // Leave in place to update old global var names
    private string GetBeatSaberRootGlobal()
    {
        if (cph.GetGlobalVar<string>("BeatSaber.BeatSaberRoot") is { } oldValue)
        {
            cph.UnsetGlobalVar("BeatSaber.BeatSaberRoot");

            if (!string.IsNullOrEmpty(oldValue))
            {
                cph.SetGlobalVar($"{GlobalVarPrefix}.BeatSaberRoot", oldValue);

                return oldValue;
            }
        }

        return cph.GetGlobalVar<string>($"{GlobalVarPrefix}.BeatSaberRoot");
    }

    private static string GetBeatSaberProcessPath() =>
        Process.GetProcessesByName("Beat Saber").FirstOrDefault()
            is { MainModule.FileName: { } fileName }
            ? Path.GetDirectoryName(fileName)
            : null;

    private static string GetGlobalVarName([CallerMemberName] string memberName = null) =>
        memberName?.TrimStart('_') is { Length: > 0 } trimmed
            ? $"{GlobalVarPrefix}.{trimmed[0].ToString().ToUpper()}{trimmed.Substring(1)}"
            : throw new InvalidOperationException(
                $"Failed to get global var name from member name for member: {memberName}."
            );

    private bool LogInvalidReasonAndReturn(string reason)
    {
        Logger.LogWarn(reason);

        return false;
    }
}
