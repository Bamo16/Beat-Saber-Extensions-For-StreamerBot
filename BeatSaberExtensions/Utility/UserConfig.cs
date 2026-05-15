using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
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

    // {Version} is a placeholder replaced by StreamerBotBuilder at build time using the <Version>
    // element from the .csproj. This value reads as the literal string "{Version}" in local builds,
    // which is expected — this code is never executed outside of StreamerBot.
    public static readonly Version Version = new Version("{Version}");

    private static readonly object _lock = new object();

    public static BeatSaberExtensionsConfig Config { get; private set; } =
        new BeatSaberExtensionsConfig();

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

    public static void InitializeUserConfig(IInlineInvokeProxy cph)
    {
        _cph = cph;
        LoadConfigValues();
    }

    public static void AddRecentExceptionMessage(string message) =>
        RecentErrorMessages = [
            .. RecentErrorMessages
                .Take(Math.Max(0, Config.KeepRecentErrorCount - 1))
                .Prepend($"[{DateTime.Now:MMM d HH:mm}] {message}"),
        ];

    public static string GetGlobalVarName([CallerMemberName] string memberName = null) =>
        memberName?.TrimStart('_') is { Length: > 0 } trimmed
            ? $"{GlobalVarPrefix}.{trimmed[0].ToString().ToUpper()}{trimmed.Substring(1)}"
            : throw new InvalidOperationException(
                $"Failed to get global var name from member name for member: {memberName}."
            );

    public static void LoadConfigValues()
    {
        lock (_lock)
        {
            try
            {
                var path = BeatSaberExtensionsConfig.ResolveDefaultPath();

                if (!File.Exists(path))
                {
                    Config = new BeatSaberExtensionsConfig();
                    Config.Save(path);
                    Logger.Log($"Created default config file at: {path}");
                }
                else
                {
                    var mtime = File.GetLastWriteTimeUtc(path);
                    if (_configValuesInitialized && mtime == _configFileMTimeUtc)
                    {
                        return;
                    }

                    Config = BeatSaberExtensionsConfig.Load(path);
                }

                _configFileMTimeUtc = File.GetLastWriteTimeUtc(path);

                Logger.LogObject(
                    Config,
                    _configValuesInitialized
                        ? "Reloaded Configuration Values"
                        : "Loaded Configuration Values",
                    truncateAfterChars: int.MaxValue
                );

                _configValuesInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    $"Failed to load config from {BeatSaberExtensionsConfig.FileName}: {ex.Message}. Defaults will be used."
                );
                // Mark initialized so we don't keep retrying on every action invocation.
                _configValuesInitialized = true;
            }
        }
    }
}
