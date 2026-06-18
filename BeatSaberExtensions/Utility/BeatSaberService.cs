using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Extensions.StringExtensions;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;
using BeatSaberExtensions.Utility.Http.BeatLeader;
using BeatSaberExtensions.Utility.Http.BeatSaver;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using BeatSaberExtensions.Utility.LazyEvaluation;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BeatSaberExtensions.Utility;

public class BeatSaberService(IInlineInvokeProxy cph) : IDisposable
{
    private const string RaidRequestorGroupName = "Raid Requestors";

    private static readonly object _beatLeaderIdLock = new object();
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

    public bool EnsureRaidRequestorsMembershipForUser(BaseUserInfo user, bool shouldBelongToGroup)
    {
        var actionVerb = shouldBelongToGroup ? "Adding" : "Removing";

        Logger.Log($"{actionVerb} raider {user.Format()} to Raid Requestors group.");

        return cph.EnsureGroupMembershipForUser(
            user,
            RaidRequestorGroupName,
            shouldBelongToGroup,
            alwaysLog: true
        );
    }

    public string GetBeatmapId(string input) => input.MatchBsrRequest(bsrIdOnly: true);

    public bool TryValidateBeatmap(string bsrId, out Beatmap beatmap, out string error)
    {
        beatmap = null;
        error = null;

        if (string.IsNullOrWhiteSpace(bsrId))
        {
            error = "Failed to validate beatmap because the provided id was null or empty.";

            return false;
        }

        var remappedId = DatabaseJson.Remaps.TryGetValue(bsrId, out var id) ? id : bsrId;
        var label = remappedId.Equals(bsrId, StringComparison.OrdinalIgnoreCase)
            ? $"\"{remappedId}\""
            : $"\"{remappedId}\" (remapped from: \"{bsrId}\")";

        if (DatabaseJson.Blacklist.Contains(remappedId))
        {
            error = $"Failed to validate beatmap id {label} because it is on the blacklist.";

            return false;
        }

        if (BeatSaverClient.GetBeatmap(remappedId) is not { } fetched)
        {
            error =
                $"Failed to validate beatmap id {label} because the beatmap could not be retrieved from BeatSaver.";

            return false;
        }

        if (
            fetched is { Metadata.LevelAuthorName: { } author }
            && DatabaseJson.BannedMappers.Contains(author)
        )
        {
            error =
                $"Failed to validate beatmap id {label} because the mapper ({author}) is on the banned mappers list.";

            return false;
        }

        beatmap = fetched;

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
                        ?.Value<bool>()
                    ?? false,
                TimeSpan.FromSeconds(5)
            )
        ).Value;

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

        var data = JObject.Parse(File.ReadAllText(path)).ToObject<DatabaseJsonInternal>();
        var beatmaps = BeatSaverClient.GetBeatmaps(data.BeatmapIds);

        return new DatabaseJson(cph, data, beatmaps);
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
                        cph.SetGlobalVar(GetGlobalVarName("BeatSaberRoot"), processValue);

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
        var globalVarName = GetGlobalVarName("BeatSaberRoot");

        if (cph.GetGlobalVar<string>("BeatSaber.BeatSaberRoot") is { } oldValue)
        {
            cph.UnsetGlobalVar("BeatSaber.BeatSaberRoot");

            if (!string.IsNullOrEmpty(oldValue))
            {
                cph.SetGlobalVar(globalVarName, oldValue);

                return oldValue;
            }
        }

        return cph.GetGlobalVar<string>(globalVarName);
    }

    private static string GetBeatSaberProcessPath() =>
        Process.GetProcessesByName("Beat Saber").FirstOrDefault()
            is { MainModule.FileName: { } fileName }
            ? Path.GetDirectoryName(fileName)
            : null;

    private static string GetGlobalVarName([CallerMemberName] string memberName = null) =>
        UserConfig.GetGlobalVarName(memberName);
}
