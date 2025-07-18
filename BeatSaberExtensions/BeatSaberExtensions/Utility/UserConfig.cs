using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.Logging;

namespace BeatSaberExtensions.Utility;

public static class UserConfig
{
    public const string GithubUrl =
        "https://github.com/Bamo16/Beat-Saber-Extensions-For-StreamerBot";
    public const string BumpEventName = "BeatSaberExtensionsBump";

    private static readonly object _lock = new object();
    private static readonly ConcurrentDictionary<string, object> _configValues = [];
    private static readonly List<(string Name, object Value)> _changes = [];

    public static readonly Version Version = new Version(0, 1, 2);

    private static bool _configValuesInitialized;
    private static Dictionary<string, object> _sbArgs;

    #region Command Ids

    public const string QueueCommandId = "243fe815-a265-4607-96ad-36a6ec5f055b";
    public const string MyQueueCommandId = "b6a0b8fa-cedf-421d-8912-1fa771090025";
    public const string WhenCommandId = "6b38768d-a84d-4beb-833e-1bc6405b310a";
    public const string BumpCommandId = "720762d9-2da3-47b5-a149-6abb93341ffc";
    public const string LookupCommandId = "b292e07e-59ff-476c-b03c-99ed05605ad0";
    public const string RaidRequestCommandId = "97eac70e-4537-4339-8f36-46b58eed97ca";
    public const string CaptureBeatLeaderCommandId = "f9303f0c-183c-459f-8982-20f8c44da5d5";
    public const string EnableCommandId = "48c2d642-01ef-4351-99c2-bbf6f89fc7b1";
    public const string DisableCommandId = "58726a1b-3421-46e0-9d03-1e975c8d93b0";
    public const string VersionCommandId = "4e45f76d-2a3b-4689-a730-c0ed0cbd6072";

    public static readonly string[] NonModCommandIds =
    [
        QueueCommandId,
        MyQueueCommandId,
        WhenCommandId,
        LookupCommandId,
        VersionCommandId,
    ];

    #endregion

    #region Response Messages

    public static string NotConfiguredMessage =>
        GetConfigValue(
            "The BeatSaber.BeatSaberRoot global variable is not currently configured. Please try running this command again while BeatSaber is running, and the variable will be set automatically."
        );
    public static string QueueEmptyMessage =>
        GetConfigValue("There aren't currently any songs in the queue.");
    public static string NonModeratorBumpMessage =>
        GetConfigValue("Only moderators can use the !bsrbump command.🚫");
    public static string BlankInputBumpMessage =>
        GetConfigValue(
            "You must provide either a BSR Id, username, or displayname for the !bsrbump command.🚫"
        );
    public static string FailedToGetBeatLeaderIdMessage =>
        GetConfigValue("Failed to get BeatLeader Id from BeatSaberPlus.");
    public static string LookupMissingBsrIdMessage =>
        GetConfigValue("You must provide a BSR Id with !bsrlookup.");
    public static string QueueStatusOpenMessage => GetConfigValue("Queue Status: OPEN✅");
    public static string QueueStatusClosedMessage => GetConfigValue("Queue Status: CLOSED🚫");
    public static string StateCommandEnabledMessage => GetConfigValue("Enabled Non-mod commands.");
    public static string StateCommandDisabledMessage =>
        GetConfigValue("Disabled Non-mod commands.");
    public static string RaidRequestBumpMessage => GetConfigValue("Raid request bump");

    #endregion

    #region Response Format Strings

    public static string InvalidInputBumpFormat =>
        GetConfigValue(
            "The provided value (\"{0}\") does not match any queued BSR Id, username, or displayname.🚫"
        );
    public static string NoUserRequestsBumpFormat =>
        GetConfigValue("There currently aren't any requests in the queue for {0}.");
    public static string SongBumpFailureFormat =>
        GetConfigValue(
            "Couldn't verify song bump success. Please confirm that {0} was bumped to the top.⚠️"
        );
    public static string SongMessageFormat =>
        GetConfigValue("!songmsg {0} {1} for {2} approved by {3}");
    public static string LookupInvalidBsrIdFormat => GetConfigValue("Invalid beatmap id: \"{0}\".");
    public static string LookupBeatmapNoFoundFormat =>
        GetConfigValue("Failed to find beatmap for id: \"{0}\".");
    public static string UserHasNoRequestsFormat =>
        GetConfigValue("{0} {1} not currently have any requests in the queue.");
    public static string LookupNoRecentScoresFormat =>
        GetConfigValue("Didn't find any recent scores by {0} on {1}.");
    public static string LookupScoreResultFormat =>
        GetConfigValue("Beatmap: {0} ({1}) ❙ {2}, played {3}.");
    public static string WhenMessageFormat =>
        GetConfigValue("{0} is at position #{1}, and is playing in {2}.");

    #endregion

    #region General Configuration Settings

    public static UsernameDisplayMode UsernameDisplayMode =>
        GetConfigValue(UsernameDisplayMode.UserLoginOnly);
    public static int DefaultQueueItemCount => GetConfigValue(5);
    public static int MaximumQueueItemCount => GetConfigValue(10);
    public static TimeSpan BeatmapCacheDuration =>
        TimeSpan.FromMinutes(GetConfigValue(30, "BeatmapCacheDurationMinutes"));
    public static TimeSpan BeatmapRefreshAfterDuration =>
        BeatmapCacheDuration
        - TimeSpan.FromSeconds(Math.Min(30, 0.2 * BeatmapCacheDuration.TotalSeconds));
    public static TimeSpan TimeBetweenSongs =>
        TimeSpan.FromSeconds(GetConfigValue(90, "SecondsBetweenSongs"));

    #endregion

    #region Song Bump Configuration Settings

    public static int BumpValidationAttempts => GetConfigValue(3);
    public static int BumpValidationDelayMs => GetConfigValue(4000);
    public static bool BumpNextRequestFromRaider => GetConfigValue(false);

    #endregion

    #region Beatmap Safe Mode Display Options

    public static bool AlwaysShowWhenCurated => GetConfigValue(true);
    public static TimeSpan MinimumAge => TimeSpan.FromDays(GetConfigValue(7, "MinimumimumAgeDays"));
    public static double MinimumScore => GetConfigValue(0.65);
    public static long MinimumUpvotes => GetConfigValue(500L);
    public static TimeSpan MinimumDuration =>
        TimeSpan.FromSeconds(GetConfigValue(90, "MinimumDurationSeconds"));

    #endregion

    public static void SetConfigValues(ActionContext context)
    {
        lock (_lock)
        {
            _sbArgs = context.SbArgs;
            _changes.Clear();

            SetConfigValue<string>(nameof(NotConfiguredMessage));
            SetConfigValue<string>(nameof(QueueEmptyMessage));
            SetConfigValue<string>(nameof(NonModeratorBumpMessage));
            SetConfigValue<string>(nameof(BlankInputBumpMessage));
            SetConfigValue<string>(nameof(FailedToGetBeatLeaderIdMessage));
            SetConfigValue<string>(nameof(LookupMissingBsrIdMessage));
            SetConfigValue<string>(nameof(QueueStatusOpenMessage));
            SetConfigValue<string>(nameof(QueueStatusClosedMessage));
            SetConfigValue<string>(nameof(StateCommandEnabledMessage));
            SetConfigValue<string>(nameof(StateCommandDisabledMessage));
            SetConfigValue<string>(nameof(RaidRequestBumpMessage));

            SetConfigValue<string>(nameof(InvalidInputBumpFormat));
            SetConfigValue<string>(nameof(NoUserRequestsBumpFormat));
            SetConfigValue<string>(nameof(SongBumpFailureFormat));
            SetConfigValue<string>(nameof(SongMessageFormat));
            SetConfigValue<string>(nameof(LookupInvalidBsrIdFormat));
            SetConfigValue<string>(nameof(LookupBeatmapNoFoundFormat));
            SetConfigValue<string>(nameof(UserHasNoRequestsFormat));
            SetConfigValue<string>(nameof(LookupNoRecentScoresFormat));
            SetConfigValue<string>(nameof(LookupScoreResultFormat));
            SetConfigValue<string>(nameof(WhenMessageFormat));

            SetConfigValue<UsernameDisplayMode>(nameof(UsernameDisplayMode));
            SetConfigValue<int>(nameof(DefaultQueueItemCount));
            SetConfigValue<int>(nameof(MaximumQueueItemCount));
            SetConfigValue<int>("BeatmapCacheDurationMinutes");
            SetConfigValue<int>("SecondsBetweenSongs");

            SetConfigValue<int>(nameof(BumpValidationAttempts));
            SetConfigValue<int>(nameof(BumpValidationDelayMs));
            SetConfigValue<bool>(nameof(BumpNextRequestFromRaider));

            SetConfigValue<bool>(nameof(AlwaysShowWhenCurated));
            SetConfigValue<int>("MinimumimumAgeDays");
            SetConfigValue<double>(nameof(MinimumScore));
            SetConfigValue<long>(nameof(MinimumUpvotes));
            SetConfigValue<int>("MinimumDurationSeconds");

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

    private static T GetConfigValue<T>(
        T defaultValue = default,
        [CallerMemberName] string memberName = null
    ) => _configValues.TryGet(memberName, out T value) ? value : defaultValue;

    private static void SetConfigValue<T>(string memberName)
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
