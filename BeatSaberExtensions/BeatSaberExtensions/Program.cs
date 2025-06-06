using System;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into streamer.bot
{
    private const string ActionName = "Beat Saber Extensions";

    private StreamerBotEventHandler _sbEventHandler;

    public void Init()
    {
        try
        {
            Logger.Init(CPH, ActionName);
            _sbEventHandler = new StreamerBotEventHandler(CPH);

            Logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            if (Logger.IsInitialized)
                Logger.HandleException(ex, setArgument: false);
            else
                CPH.HandleNullLoggerOnInit(ex, nameof(Logger), ActionName);
        }
    }

    public void Dispose() => _sbEventHandler?.Dispose();

    public bool Execute()
    {
        Logger.LogActionStart(args, out var executeSuccess, out var sbArgs, out var eventType);

        try
        {
            UserConfig.SetConfigValues(sbArgs);

            var message = _sbEventHandler.HandleStreamerBotEvent(eventType, sbArgs);

            if (!string.IsNullOrEmpty(message))
                CPH.SendMessage(message);

            executeSuccess = true;
        }
        catch (Exception ex)
        {
            Logger.HandleException(ex);
        }
        finally
        {
            Logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }
}
