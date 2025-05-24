using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Enums;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Utility.Logging;

namespace BeatSaberExtensions.Utility;

public static class UserConfig
{
    private static readonly object _beatSaberServiceLock = new();
    private static readonly ConcurrentDictionary<string, object> _beatSaberServiceValues = [];

    private static StreamerBotLogger _logger;
    private static bool _beatSaberServiceValuesInitialized;
    private static Dictionary<string, object> _sbArgs;

    private static readonly List<(string Name, object Value)> _changes = [];

    #region Private Config Values

    private static int BeatmapCacheDurationMinutes => GetConfigValue(30);
    private static int SecondsBetweenSongs => GetConfigValue(90);
    private static int ClearRaidRequestorsAfterMinutes => GetConfigValue(30);
    private static int MinimumimumAgeDays => GetConfigValue(7);
    private static int MinimumDurationSeconds => GetConfigValue(90);

    #endregion

    #region Response Messages

    public static string NotConfiguredMessage =>
        GetConfigValue(
            "The BeatSaber.BeatSaberRoot global variable is not currently configured. Please try running this command again while BeatSaber is running, and the variable will be set automatically."
        );
    public static string QueueEmptyMessage =>
        GetConfigValue("There aren't currently any songs in the queue.");
    public static string UserHasNoRequestsMessage =>
        GetConfigValue("Only moderators can use the !bsrbump command.🚫");
    public static string NonModeratorBumpMessage =>
        GetConfigValue("Only moderators can use the !bsrbump command.");
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

    // add to arguments
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

    // add to arguments
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
    public static int MaximumQueueItemCount => GetConfigValue(10);
    public static TimeSpan BeatmapCacheDuration =>
        TimeSpan.FromMinutes(BeatmapCacheDurationMinutes);
    public static TimeSpan TimeBetweenSongs => TimeSpan.FromSeconds(SecondsBetweenSongs);

    #endregion

    #region Song Bump Configuration Settings

    public static int BumpValidationAttempts => GetConfigValue(2);
    public static int BumpValidationDelayMs => GetConfigValue(4000);
    public static bool BumpNextRequestFromRaider => GetConfigValue(false);
    public static TimeSpan ClearRaidRequestorsAfter =>
        TimeSpan.FromMinutes(ClearRaidRequestorsAfterMinutes);

    #endregion

    #region Beatmap Safe Mode Display Options

    public static bool AlwaysShowWhenCurated => GetConfigValue(true);
    public static TimeSpan MinimumAge => TimeSpan.FromDays(MinimumimumAgeDays);
    public static double MinimumScore => GetConfigValue(0.65);
    public static long MinimumUpvotes => GetConfigValue(500L);
    public static TimeSpan MinimumDuration => TimeSpan.FromSeconds(MinimumDurationSeconds);

    #endregion

    public static void Init(StreamerBotLogger logger) => _logger = logger;

    public static void SetConfigValues(Dictionary<string, object> sbArgs)
    {
        lock (_beatSaberServiceLock)
        {
            _sbArgs = new Dictionary<string, object>(sbArgs);
            _changes.Clear();

            SetConfigValue<string>(nameof(NotConfiguredMessage));
            SetConfigValue<string>(nameof(QueueEmptyMessage));
            SetConfigValue<string>(nameof(UserHasNoRequestsMessage));
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
            SetConfigValue<string>(nameof(LookupNoRecentScoresFormat));
            SetConfigValue<string>(nameof(LookupScoreResultFormat));
            SetConfigValue<string>(nameof(WhenMessageFormat));

            SetConfigValue<UsernameDisplayMode>(nameof(UsernameDisplayMode));
            SetConfigValue<int>(nameof(MaximumQueueItemCount));
            SetConfigValue<int>(nameof(BeatmapCacheDurationMinutes));
            SetConfigValue<int>(nameof(SecondsBetweenSongs));

            SetConfigValue<int>(nameof(BumpValidationAttempts));
            SetConfigValue<int>(nameof(BumpValidationDelayMs));
            SetConfigValue<bool>(nameof(BumpNextRequestFromRaider));
            SetConfigValue<int>(nameof(ClearRaidRequestorsAfterMinutes));

            SetConfigValue<bool>(nameof(AlwaysShowWhenCurated));
            SetConfigValue<int>(nameof(MinimumimumAgeDays));
            SetConfigValue<double>(nameof(MinimumScore));
            SetConfigValue<long>(nameof(MinimumUpvotes));
            SetConfigValue<int>(nameof(MinimumDurationSeconds));

            if (_changes is { Count: > 0 })
            {
                var logObject = _changes.ToDictionary(item => item.Name, item => item.Value);
                var logMessageLabel = string.Format(
                    "{0} Configuration Values",
                    !_beatSaberServiceValuesInitialized ? "Initialized" : "Updated"
                );

                _logger.LogObject(logObject, logMessageLabel, truncateAfterChars: int.MaxValue);
            }

            if (_beatSaberServiceValuesInitialized is false)
                _beatSaberServiceValuesInitialized = true;
        }
    }

    private static T GetConfigValue<T>(
        T defaultValue = default,
        [CallerMemberName] string memberName = null
    ) => _beatSaberServiceValues.TryGetArg(memberName, out T value) ? value : defaultValue;

    private static void SetConfigValue<T>(string memberName)
    {
        var argExists = _sbArgs.TryGetArg(memberName, out T newValue);

        if (!_beatSaberServiceValuesInitialized && !argExists)
        {
            _logger.LogWarn(
                $"Missing StreamerBot argument: \"{memberName}\". Default value will be used."
            );

            return;
        }

        if (
            argExists
            && (
                !_beatSaberServiceValuesInitialized
                || !_beatSaberServiceValues.TryGetArg(memberName, out T currentValue)
                || !currentValue.Equals(newValue)
            )
        )
        {
            _beatSaberServiceValues[memberName] = newValue;
            _changes.Add((memberName, newValue));
        }
    }
}
