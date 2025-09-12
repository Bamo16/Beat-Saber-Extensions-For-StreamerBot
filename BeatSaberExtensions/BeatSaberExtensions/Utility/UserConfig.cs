using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility;

public static class UserConfig
{
    public const string GithubUrl =
        "https://github.com/Bamo16/Beat-Saber-Extensions-For-StreamerBot";
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
    private static readonly object _lock = new object();
    private static readonly ConcurrentDictionary<string, object> _configValues = [];
    private static readonly List<(string Name, object Value)> _changes = [];

    public static readonly Version Version = new Version(0, 1, 3);

    public static List<string> RecentErrorMessages
    {
        get => CPH.GetCustomGlobalVar<List<string>>(GetGlobalVarName(), defaultValue: []);
        private set => CPH.SetCustomGlobalVar(GetGlobalVarName(), value);
    }

    private static IInlineInvokeProxy _cph;
    private static bool _configValuesInitialized;
    private static Dictionary<string, object> _sbArgs;

    private static IInlineInvokeProxy CPH =>
        _cph ?? throw new InvalidOperationException($"{nameof(CPH)} is null.");

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

    public static string NotConfiguredMessage =>
        Get(
            "The BeatSaber.BeatSaberRoot global variable is not currently configured. Please try running this command again while BeatSaber is running, and the variable will be set automatically."
        );
    public static string QueueEmptyMessage => Get("There aren't currently any songs in the queue.");
    public static string NonModeratorBumpMessage =>
        Get("Only moderators can use the !bsrbump command.🚫");
    public static string BlankInputBumpMessage =>
        Get(
            "You must provide either a BSR Id, username, or displayname for the !bsrbump command.🚫"
        );
    public static string FailedToGetBeatLeaderIdMessage =>
        Get("Failed to get BeatLeader Id from BeatSaberPlus.");
    public static string LookupMissingBsrIdMessage =>
        Get("You must provide a BSR Id with !bsrlookup.");
    public static string QueueStatusOpenMessage => Get("Queue Status: OPEN✅");
    public static string QueueStatusClosedMessage => Get("Queue Status: CLOSED🚫");
    public static string StateCommandEnabledMessage => Get("Enabled Non-mod commands.");
    public static string StateCommandDisabledMessage => Get("Disabled Non-mod commands.");
    public static string RaidRequestBumpMessage => Get("Raid request bump");

    #endregion

    #region Response Format Strings

    public static string InvalidInputBumpFormat =>
        Get(
            "The provided value (\"{0}\") does not match any queued BSR Id, username, or displayname.🚫"
        );
    public static string NoUserRequestsBumpFormat =>
        Get("There currently aren't any requests in the queue for {0}.");
    public static string SongBumpFailureFormat =>
        Get("Couldn't verify song bump success. Please confirm that {0} was bumped to the top.⚠️");
    public static string SongMessageFormat => Get("!songmsg {0} {1} for {2} approved by {3}");
    public static string LookupInvalidBsrIdFormat => Get("Invalid beatmap id: \"{0}\".");
    public static string LookupBeatmapNoFoundFormat =>
        Get("Failed to find beatmap for id: \"{0}\".");
    public static string UserHasNoRequestsFormat =>
        Get("{0} {1} not currently have any requests in the queue.");
    public static string LookupNoRecentScoresFormat =>
        Get("Didn't find any recent scores by {0} on {1}.");
    public static string LookupScoreResultFormat => Get("Beatmap: {0} ({1}) ❙ {2}, played {3}.");
    public static string WhenMessageFormat =>
        Get("{0} is at position #{1}, and is playing in {2}.");

    #endregion

    #region General Configuration Settings

    public static bool AllowBotWhispers = Get(true);
    public static UsernameDisplayMode UsernameDisplayMode => Get(UsernameDisplayMode.UserLoginOnly);
    public static int DefaultQueueItemCount => Get(5);
    public static int MaximumQueueItemCount => Get(10);
    public static TimeSpan BeatmapCacheDuration =>
        TimeSpan.FromMinutes(Get(30, "BeatmapCacheDurationMinutes"));
    public static TimeSpan BeatmapRefreshAfterDuration =>
        BeatmapCacheDuration
        - TimeSpan.FromSeconds(Math.Min(30, 0.2 * BeatmapCacheDuration.TotalSeconds));
    public static TimeSpan TimeBetweenSongs => TimeSpan.FromSeconds(Get(90, "SecondsBetweenSongs"));
    public static int KeeoRecentErrorCount => Get(10);

    #endregion

    #region Song Bump Configuration Settings

    public static int BumpValidationAttempts => Get(3);
    public static int BumpValidationDelayMs => Get(4000);
    public static bool BumpNextRequestFromRaider => Get(false);

    #endregion

    #region Beatmap Safe Mode Display Options

    public static bool AlwaysShowWhenCurated => Get(true);
    public static TimeSpan MinimumAge => TimeSpan.FromDays(Get(7, "MinimumimumAgeDays"));
    public static double MinimumScore => Get(0.65);
    public static long MinimumUpvotes => Get(500L);
    public static TimeSpan MinimumDuration =>
        TimeSpan.FromSeconds(Get(90, "MinimumDurationSeconds"));

    #endregion

    public static void InitializeUserConfig(IInlineInvokeProxy cph) => _cph = cph;

    public static void AddRecentExceptionMessage(string message) =>
        RecentErrorMessages = [
            .. RecentErrorMessages.Take(9).Prepend($"[{DateTime.Now:MMM d HH:mm}] {message}"),
        ];

    public static string GetGlobalVarName([CallerMemberName] string memberName = null) =>
        memberName?.TrimStart('_') is { Length: > 0 } trimmed
            ? $"{GlobalVarPrefix}.{trimmed[0].ToString().ToUpper()}{trimmed.Substring(1)}"
            : throw new InvalidOperationException(
                $"Failed to get global var name from member name for member: {memberName}."
            );

    public static void SetConfigValues(ActionContext context)
    {
        lock (_lock)
        {
            _sbArgs = context.Args;
            _changes.Clear();

            Set<string>(nameof(NotConfiguredMessage));
            Set<string>(nameof(QueueEmptyMessage));
            Set<string>(nameof(NonModeratorBumpMessage));
            Set<string>(nameof(BlankInputBumpMessage));
            Set<string>(nameof(FailedToGetBeatLeaderIdMessage));
            Set<string>(nameof(LookupMissingBsrIdMessage));
            Set<string>(nameof(QueueStatusOpenMessage));
            Set<string>(nameof(QueueStatusClosedMessage));
            Set<string>(nameof(StateCommandEnabledMessage));
            Set<string>(nameof(StateCommandDisabledMessage));
            Set<string>(nameof(RaidRequestBumpMessage));

            Set<string>(nameof(InvalidInputBumpFormat));
            Set<string>(nameof(NoUserRequestsBumpFormat));
            Set<string>(nameof(SongBumpFailureFormat));
            Set<string>(nameof(SongMessageFormat));
            Set<string>(nameof(LookupInvalidBsrIdFormat));
            Set<string>(nameof(LookupBeatmapNoFoundFormat));
            Set<string>(nameof(UserHasNoRequestsFormat));
            Set<string>(nameof(LookupNoRecentScoresFormat));
            Set<string>(nameof(LookupScoreResultFormat));
            Set<string>(nameof(WhenMessageFormat));

            Set<bool>(nameof(AllowBotWhispers));
            Set<UsernameDisplayMode>(nameof(UsernameDisplayMode));
            Set<int>(nameof(DefaultQueueItemCount));
            Set<int>(nameof(MaximumQueueItemCount));
            Set<int>("BeatmapCacheDurationMinutes");
            Set<int>("SecondsBetweenSongs");
            Set<int>(nameof(KeeoRecentErrorCount));

            Set<int>(nameof(BumpValidationAttempts));
            Set<int>(nameof(BumpValidationDelayMs));
            Set<bool>(nameof(BumpNextRequestFromRaider));

            Set<bool>(nameof(AlwaysShowWhenCurated));
            Set<int>("MinimumimumAgeDays");
            Set<double>(nameof(MinimumScore));
            Set<long>(nameof(MinimumUpvotes));
            Set<int>("MinimumDurationSeconds");

            if (_changes is { Count: > 0 })
            {
                var logObject = _changes.ToDictionary(item => item.Name, item => item.Value);
                var logMessageLabel = string.Format(
                    "{0} Configuration Values",
                    !_configValuesInitialized ? "Initialized" : "Updated"
                );

                Logger.LogObject(logObject, logMessageLabel, truncateAfterChars: int.MaxValue);
            }

            if (_configValuesInitialized is false)
            {
                _configValuesInitialized = true;
            }
        }
    }

    private static T Get<T>(
        T defaultValue = default,
        [CallerMemberName] string memberName = null
    ) => _configValues.TryGet(memberName, out T value) ? value : defaultValue;

    private static void Set<T>(string memberName)
    {
        var argExists = _sbArgs.TryGet(memberName, out T newValue);

        if (!_configValuesInitialized && !argExists)
        {
            Logger.LogWarn(
                $"Missing StreamerBot argument: \"{memberName}\". Default value will be used."
            );

            return;
        }

        if (
            argExists
            && (
                !_configValuesInitialized
                || !_configValues.TryGet(memberName, out T currentValue)
                || !currentValue.Equals(newValue)
            )
        )
        {
            _configValues[memberName] = newValue;
            _changes.Add((memberName, newValue));
        }
    }
}
