using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility;

public static class UserConfig
{
    public const string GithubUrl =
        "https://github.com/Bamo16/Beat-Saber-Extensions-For-StreamerBot";
    public const string ConfigFileName = "BeatSaberExtensions.config.json";
    private const string GlobalVarPrefix = "BeatSaberExtensions";
    public const string RefreshLastLiveTimestampTimerId = "fe0b585f-4d61-458d-897b-5d6e876cc574";
    public const string AllKnownUsersGroup = "All Known Users";
    public const string LocalizedDisplayUsersGroup = "Localized DisplayName Users";
    public const string StreamerBotUsersGroup = "StreamerBot Users";
    public const string RaidRequestorGroup = "Raid Requestors";
    public const string LogUsersGroup = "BSR Extensions Log Users";

    public static readonly string[] EnsureGroupsExist =
    [
        RaidRequestorGroup,
        AllKnownUsersGroup,
        LocalizedDisplayUsersGroup,
        StreamerBotUsersGroup,
        LogUsersGroup,
    ];

    public static readonly Version Version = new Version(0, 2, 0);

    private static readonly object _lock = new object();
    private static readonly ConcurrentDictionary<string, object> _configValues = [];

    private static readonly JsonSerializerSettings _serializerSettings =
        new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new StringEnumConverter() },
        };

    private static readonly JsonSerializer _serializer = JsonSerializer.Create(
        _serializerSettings
    );

    private static readonly Dictionary<string, object> _defaults = new()
    {
        // Response Messages
        [nameof(NotConfiguredMessage)] =
            "The BeatSaber.BeatSaberRoot global variable is not currently configured. Please try running this command again while BeatSaber is running, and the variable will be set automatically.",
        [nameof(QueueEmptyMessage)] = "There aren't currently any songs in the queue.",
        [nameof(NonModeratorBumpMessage)] = "Only moderators can use the !bsrbump command.🚫",
        [nameof(BlankInputBumpMessage)] =
            "You must provide either a BSR Id, username, or displayname for the !bsrbump command.🚫",
        [nameof(FailedToGetBeatLeaderIdMessage)] = "Failed to get BeatLeader Id from BeatSaberPlus.",
        [nameof(LookupMissingBsrIdMessage)] = "You must provide a BSR Id with !bsrlookup.",
        [nameof(QueueStatusOpenMessage)] = "Queue Status: OPEN✅",
        [nameof(QueueStatusClosedMessage)] = "Queue Status: CLOSED🚫",
        [nameof(StateCommandEnabledMessage)] = "Enabled Non-mod commands.",
        [nameof(StateCommandDisabledMessage)] = "Disabled Non-mod commands.",
        [nameof(RaidRequestBumpMessage)] = "Raid request bump",
        // Response Format Strings
        [nameof(InvalidInputBumpFormat)] =
            "The provided value (\"{0}\") does not match any queued BSR Id, username, or displayname.🚫",
        [nameof(NoUserRequestsBumpFormat)] =
            "There currently aren't any requests in the queue for {0}.",
        [nameof(SongBumpFailureFormat)] =
            "Couldn't verify song bump success. Please confirm that {0} was bumped to the top.⚠️",
        [nameof(SongMessageFormat)] = "!songmsg {0} {1} for {2} approved by {3}",
        [nameof(LookupInvalidBsrIdFormat)] = "Invalid beatmap id: \"{0}\".",
        [nameof(LookupBeatmapNoFoundFormat)] = "Failed to find beatmap for id: \"{0}\".",
        [nameof(UserHasNoRequestsFormat)] =
            "{0} {1} not currently have any requests in the queue.",
        [nameof(LookupNoRecentScoresFormat)] = "Didn't find any recent scores by {0} on {1}.",
        [nameof(LookupScoreResultFormat)] = "Beatmap: {0} ({1}) ❙ {2}, played {3}.",
        [nameof(WhenMessageFormat)] = "{0} is at position #{1}, and is playing in {2}.",
        // General Configuration Settings
        [nameof(AllowBotWhispers)] = true,
        [nameof(UsernameDisplayMode)] = Enums.UsernameDisplayMode.UserLoginOnly,
        [nameof(DefaultQueueItemCount)] = 5,
        [nameof(MaximumQueueItemCount)] = 10,
        ["BeatmapCacheDurationMinutes"] = 30,
        ["SecondsBetweenSongs"] = 90,
        [nameof(KeepRecentErrorCount)] = 10,
        // Song Bump Configuration Settings
        [nameof(BumpValidationAttempts)] = 3,
        [nameof(BumpValidationDelayMs)] = 4000,
        [nameof(BumpNextRequestFromRaider)] = false,
        // Beatmap Safe Mode Display Options
        [nameof(AlwaysShowWhenCurated)] = true,
        ["MinimumAgeDays"] = 7,
        [nameof(MinimumScore)] = 0.65,
        [nameof(MinimumUpvotes)] = 500L,
        ["MinimumDurationSeconds"] = 90,
    };

    private static IInlineInvokeProxy _cph;
    private static bool _configValuesInitialized;
    private static DateTime _configFileMTimeUtc = DateTime.MinValue;

    private static IInlineInvokeProxy CPH =>
        _cph ?? throw new InvalidOperationException($"{nameof(CPH)} is null.");

    public static List<string> RecentErrorMessages
    {
        get => CPH.GetCustomGlobalVar<List<string>>(GetGlobalVarName(), defaultValue: []);
        private set => CPH.SetCustomGlobalVar(GetGlobalVarName(), value);
    }

    #region Command Ids

    public const string QueueCommandId = "243fe815-a265-4607-96ad-36a6ec5f055b";
    public const string MyQueueCommandId = "b6a0b8fa-cedf-421d-8912-1fa771090025";
    public const string WhenCommandId = "6b38768d-a84d-4beb-833e-1bc6405b310a";
    public const string LastBeatmapCommandId = "c03ca6a3-5771-4e0c-9131-c3f2a9444777";
    public const string BumpCommandId = "720762d9-2da3-47b5-a149-6abb93341ffc";
    public const string LookupCommandId = "b292e07e-59ff-476c-b03c-99ed05605ad0";
    public const string RaidRequestCommandId = "97eac70e-4537-4339-8f36-46b58eed97ca";
    public const string LogsCommandId = "2cb292c7-390b-4f28-be22-bd144cac4739";
    public const string CaptureBeatLeaderCommandId = "f9303f0c-183c-459f-8982-20f8c44da5d5";
    public const string AddUserToGroupsCommandId = "e88c593e-f6a7-4193-bce8-7258626b803b";
    public const string EnableCommandId = "48c2d642-01ef-4351-99c2-bbf6f89fc7b1";
    public const string DisableCommandId = "58726a1b-3421-46e0-9d03-1e975c8d93b0";
    public const string VersionCommandId = "4e45f76d-2a3b-4689-a730-c0ed0cbd6072";

    public static readonly string[] NonModCommandIds =
    [
        QueueCommandId,
        MyQueueCommandId,
        WhenCommandId,
        LastBeatmapCommandId,
        LookupCommandId,
        VersionCommandId,
    ];

    #endregion

    #region Response Messages

    public static string NotConfiguredMessage => Get<string>();
    public static string QueueEmptyMessage => Get<string>();
    public static string NonModeratorBumpMessage => Get<string>();
    public static string BlankInputBumpMessage => Get<string>();
    public static string FailedToGetBeatLeaderIdMessage => Get<string>();
    public static string LookupMissingBsrIdMessage => Get<string>();
    public static string QueueStatusOpenMessage => Get<string>();
    public static string QueueStatusClosedMessage => Get<string>();
    public static string StateCommandEnabledMessage => Get<string>();
    public static string StateCommandDisabledMessage => Get<string>();
    public static string RaidRequestBumpMessage => Get<string>();

    #endregion

    #region Response Format Strings

    public static string InvalidInputBumpFormat => Get<string>();
    public static string NoUserRequestsBumpFormat => Get<string>();
    public static string SongBumpFailureFormat => Get<string>();
    public static string SongMessageFormat => Get<string>();
    public static string LookupInvalidBsrIdFormat => Get<string>();
    public static string LookupBeatmapNoFoundFormat => Get<string>();
    public static string UserHasNoRequestsFormat => Get<string>();
    public static string LookupNoRecentScoresFormat => Get<string>();
    public static string LookupScoreResultFormat => Get<string>();
    public static string WhenMessageFormat => Get<string>();

    #endregion

    #region General Configuration Settings

    public static bool AllowBotWhispers => Get<bool>();
    public static UsernameDisplayMode UsernameDisplayMode => Get<UsernameDisplayMode>();
    public static int DefaultQueueItemCount => Get<int>();
    public static int MaximumQueueItemCount => Get<int>();
    public static TimeSpan BeatmapCacheDuration =>
        TimeSpan.FromMinutes(GetByKey<int>("BeatmapCacheDurationMinutes"));
    public static TimeSpan BeatmapRefreshAfterDuration =>
        BeatmapCacheDuration
        - TimeSpan.FromSeconds(Math.Min(30, 0.2 * BeatmapCacheDuration.TotalSeconds));
    public static TimeSpan TimeBetweenSongs =>
        TimeSpan.FromSeconds(GetByKey<int>("SecondsBetweenSongs"));
    public static int KeepRecentErrorCount => Get<int>();

    #endregion

    #region Song Bump Configuration Settings

    public static int BumpValidationAttempts => Get<int>();
    public static int BumpValidationDelayMs => Get<int>();
    public static bool BumpNextRequestFromRaider => Get<bool>();

    #endregion

    #region Beatmap Safe Mode Display Options

    public static bool AlwaysShowWhenCurated => Get<bool>();
    public static TimeSpan MinimumAge => TimeSpan.FromDays(GetByKey<int>("MinimumAgeDays"));
    public static double MinimumScore => Get<double>();
    public static long MinimumUpvotes => Get<long>();
    public static TimeSpan MinimumDuration =>
        TimeSpan.FromSeconds(GetByKey<int>("MinimumDurationSeconds"));

    #endregion

    public static void InitializeUserConfig(IInlineInvokeProxy cph)
    {
        _cph = cph;
        LoadConfigValues();
    }

    public static void AddRecentExceptionMessage(string message) =>
        RecentErrorMessages = [
            .. RecentErrorMessages
                .Take(Math.Max(0, KeepRecentErrorCount - 1))
                .Prepend($"[{DateTime.Now:MMM d HH:mm}] {message}"),
        ];

    public static string GetGlobalVarName([CallerMemberName] string memberName = null) =>
        memberName?.TrimStart('_') is { Length: > 0 } trimmed
            ? $"{GlobalVarPrefix}.{trimmed[0].ToString().ToUpper()}{trimmed.Substring(1)}"
            : throw new InvalidOperationException(
                $"Failed to get global var name from member name for member: {memberName}."
            );

    public static string ResolveConfigFilePath() =>
        Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

    public static void LoadConfigValues()
    {
        lock (_lock)
        {
            try
            {
                var path = ResolveConfigFilePath();

                if (!File.Exists(path))
                {
                    WriteDefaultConfigFile(path);
                    Logger.Log($"Created default config file at: {path}");
                }

                var mtime = File.GetLastWriteTimeUtc(path);
                if (_configValuesInitialized && mtime == _configFileMTimeUtc)
                {
                    return;
                }

                var raw =
                    JsonConvert.DeserializeObject<Dictionary<string, JToken>>(
                        File.ReadAllText(path),
                        _serializerSettings
                    ) ?? [];

                var changes = new List<(string Name, object Value)>();

                foreach (var kvp in _defaults)
                {
                    var key = kvp.Key;
                    var defaultValue = kvp.Value;
                    var value = ResolveValue(raw, key, defaultValue);

                    if (
                        !_configValues.TryGetValue(key, out var current)
                        || !object.Equals(current, value)
                    )
                    {
                        _configValues[key] = value;
                        changes.Add((key, value));
                    }
                }

                _configFileMTimeUtc = mtime;

                if (changes is { Count: > 0 })
                {
                    Logger.LogObject(
                        changes.ToDictionary(c => c.Name, c => c.Value),
                        _configValuesInitialized
                            ? "Reloaded Configuration Values"
                            : "Loaded Configuration Values",
                        truncateAfterChars: int.MaxValue
                    );
                }

                _configValuesInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"Failed to load config from {ConfigFileName}: {ex.Message}. Defaults will be used."
                );
                // Mark initialized so we don't keep retrying on every action invocation.
                _configValuesInitialized = true;
            }
        }
    }

    private static object ResolveValue(
        Dictionary<string, JToken> raw,
        string key,
        object defaultValue
    )
    {
        if (!raw.TryGetValue(key, out var token) || token is null || token.Type is JTokenType.Null)
        {
            return defaultValue;
        }

        try
        {
            return token.ToObject(defaultValue.GetType(), _serializer) ?? defaultValue;
        }
        catch (Exception ex)
        {
            Logger.LogWarn(
                $"Failed to deserialize config value for \"{key}\": {ex.Message}. Default will be used."
            );

            return defaultValue;
        }
    }

    private static void WriteDefaultConfigFile(string path)
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(_defaults, _serializerSettings));
    }

    private static T Get<T>([CallerMemberName] string memberName = null) => GetByKey<T>(memberName);

    private static T GetByKey<T>(string key) =>
        _configValues.TryGetValue(key, out var value) && value is T t ? t : (T)_defaults[key];
}
