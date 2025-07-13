using System;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.Arguments;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into streamer.bot
{
    private const string ActionName = "Beat Saber Extensions";

    private static readonly Predicate<ActionContext> _noOpPredicate = (context) =>
        context.IsBroadcaster(context.Caller)
        && context.CommandId is UserConfig.RaidRequestCommandId;

    private StreamerBotEventHandler _sbEventHandler;

    public void Init()
    {
        try
        {
            Logger.Init(CPH, ActionName, _noOpPredicate);
            _sbEventHandler = new StreamerBotEventHandler(CPH);

            Logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex, setArgument: false);
        }
    }

    public void Dispose() => _sbEventHandler?.Dispose();

    public bool Execute() => _sbEventHandler.HandleStreamerBotAction(args);

    public bool BumpRequestForUser() =>
        _sbEventHandler.HandleStreamerBotAction(args, fromExecuteMethod: true);
}
