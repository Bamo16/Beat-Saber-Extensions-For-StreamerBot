using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

public class BeatSaberService(IInlineInvokeProxy cph, StreamerBotLogger logger) : IDisposable
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
    private JsonSerializer _databaseJsonSerializer;
    private Cached<DatabaseJson> _databaseJson;
    private BeatSaverClient _beatSaverClient;
    private BeatLeaderClient _beatLeaderClient;
    private bool _waitingForBeatLeaderId;

    public bool IsConfigured => !string.IsNullOrEmpty(GetBeatSaberRoot());
    public BeatSaverClient BeatSaverClient => _beatSaverClient ??= new BeatSaverClient(logger);
    public BeatLeaderClient BeatLeaderClient => _beatLeaderClient ??= new BeatLeaderClient(logger);
    public DatabaseJson DatabaseJson => GetDatabaseJson();
    public List<QueueItem> Queue => DatabaseJson.Queue;

    public string BeatLeaderId
    {
        get
        {
            if (TryGetBeatLeaderIdGlobal(out var beatLeaderId))
            {
                logger.Log($"Retrieved BeatLeaderId: {beatLeaderId}");

                return beatLeaderId;
            }

            if (_waitingForBeatLeaderId)
            {
                logger.LogWarn(
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
                    logger.Log($"Retrieved BeatLeaderId inside lock: {beatLeaderId}");

                    return beatLeaderId;
                }

                try
                {
                    _waitingForBeatLeaderId = true;

                    logger.Log("Sending !bsprofile command to retrieve BeatLeaderId");

                    cph.SendMessage("!bsprofile");

                    if (TryWaitForBeatLeaderId(out beatLeaderId))
                    {
                        logger.Log($"Successfully retrieved BeatLeaderId: {beatLeaderId}");

                        return beatLeaderId;
                    }

                    logger.LogWarn("Failed to retrieve BeatLeaderId within 15 seconds");
                }
                catch (Exception ex)
                {
                    logger.HandleException(ex);
                }
                finally
                {
                    _waitingForBeatLeaderId = false;
                }

                return null;
            }
        }
        set => cph.SetGlobalVar(GetGlobalVarName(), value);
    }

    public bool GetQueueState() =>
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

    public void SetBeatLeaderIdGlobal(string beatLeaderId) => BeatLeaderId = beatLeaderId;

    public bool UserIsInRaidRequestors(BaseUserInfo user) =>
        user is { UserId: { } userId }
        && cph.UserIdInGroup(userId, Platform.Twitch, RaidRequestorGroupName);

    public bool EnsureRaidRequestorsMembershipForUser(
        BaseUserInfo user,
        bool shouldBelongToGroup
    ) => cph.EnsureGroupMembershipForUser(user, RaidRequestorGroupName, shouldBelongToGroup);

    public string GetBeatmapId(string input) =>
        _beatmapIdPattern.Match(input.ToLowerInvariant()).Groups["Id"]
            is { Success: true, Value: { } value }
            ? value
            : null;

    public bool ValidateBeatmapId(string id)
    {
        var sanitzedId = GetBeatmapId(id) is { } validId ? Remap(validId) : null;

        if (
            // Invalid BSR ID
            sanitzedId is null
            // BSR ID is not blacklist
            || DatabaseJson.Blacklist.Contains(sanitzedId)
            // BSR ID was already requested recently
            || DatabaseJson.History.Contains(sanitzedId)
        )
        {
            return false;
        }

        var beatmap = _beatSaverClient.GetBeatmap(sanitzedId);

        // BSR ID belongs to banned mapper
        if (DatabaseJson.BannedMappers.Contains(beatmap.Metadata.LevelAuthorName))
        {
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _beatSaverClient?.Dispose();
        _beatLeaderClient?.Dispose();
    }

    private string Remap(string id) =>
        DatabaseJson.Remaps.TryGetValue(id, out var remap) ? remap : id;

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

    private bool TryGetBeatLeaderIdGlobal(out string beatLeaderId)
    {
        beatLeaderId = cph.GetGlobalVar<string>(GetGlobalVarName(nameof(BeatLeaderId)));

        return beatLeaderId is not null;
    }

    private string GetBeatSaberPlusFilePath(string fileName) =>
        IsConfigured
            ? Path.Combine(GetBeatSaberRoot(), "UserData", "BeatSaberPlus", "ChatRequest", fileName)
            : null;

    private DatabaseJson GetDatabaseJson() =>
        (
            _databaseJson ??= new Cached<DatabaseJson>(
                () =>
                    GetBeatSaberPlusFilePath("Database.json") is { } path
                    && !string.IsNullOrEmpty(path)
                        ? (
                            _databaseJsonSerializer ??= new DatabaseJsonConverter(
                                cph,
                                BeatSaverClient
                            ).GetSerializer()
                        ).Deserialize<DatabaseJson>(
                            new JsonTextReader(new StringReader(File.ReadAllText(path)))
                        )
                        : throw new JsonSerializationException(
                            "Failed to deserialize Database.json."
                        ),
                TimeSpan.FromMilliseconds(1000)
            )
        ).Value;

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

    private static string GetGlobalVarName([CallerMemberName] string memberName = "") =>
        memberName?.TrimStart('_') is { Length: > 0 } trimmed
            ? $"{GlobalVarPrefix}.{trimmed[0].ToString().ToUpper()}{trimmed.Substring(1)}"
            : throw new InvalidOperationException(
                $"Failed to get global var name from member name for member: {memberName}."
            );
}
