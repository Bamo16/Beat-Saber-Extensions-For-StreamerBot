using System;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;

public class CPHInline
#if OUTSIDE_STREAMERBOT
    : CPHInlineBase
#endif
{
    private const string ActionName = "Beat Saber Extensions";

    private GroupManager _groupManager;
    private StreamerBotEventHandler _sbEventHandler;

    public void Init()
    {
        try
        {
            Logger.Init(CPH, ActionName);
            UserConfig.InitializeUserConfig(CPH);

            _groupManager = new GroupManager(CPH);
            _groupManager.InitializeGroups();

            _sbEventHandler = new StreamerBotEventHandler(CPH);

            Logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex, setArgument: false);
        }
    }

    public void Dispose() => _sbEventHandler?.Dispose();

    // Default entrypoint
    public bool Execute() => _sbEventHandler.HandleStreamerBotAction(new ActionContext(CPH, args));

    // Execute C# Method entrypoint for custom song bump for user
    public bool BumpRequestForUser() =>
        _sbEventHandler.HandleStreamerBotAction(new ActionContext(CPH, args), isCustomBump: true);

    // Execute C# Method entrypoint for group manager triggers
    public bool HandleGroupManagerTrigger() =>
        _groupManager.HandleGroupManagerAction(new ActionContext(CPH, args));
}
