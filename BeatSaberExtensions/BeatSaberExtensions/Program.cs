using System;
using System.Collections.Generic;
using BeatSaberExtensions.Extensions.DictionaryExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Common.Events;
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

        // Exit immediately when !bsr Raider Request is triggered by the broadcaster account.
        if (executeSuccess is true)
        {
            return true;
        }

        try
        {
            UserConfig.SetConfigValues(sbArgs);

            var message = HandleStreamerBotEvent(eventType, sbArgs);

            if (!string.IsNullOrEmpty(message))
            {
                CPH.SendMessage(message);
            }

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

    public string HandleStreamerBotEvent(EventType eventType, Dictionary<string, object> sbArgs)
    {
        if (eventType is EventType.TwitchRaid)
        {
            return _sbEventHandler.HandleTwitchRaid(sbArgs);
        }

        if (eventType is EventType.CommandTriggered)
        {
            var commandId = sbArgs.GetArgOrDefault("commandId", string.Empty);

            if (_sbEventHandler.Actions.TryGetValue(commandId, out var action))
            {
                return action.Invoke(sbArgs);
            }

            var command = sbArgs.GetArgOrDefault<string>("command");

            throw new InvalidOperationException(
                $"Unsupported commandId: \"{commandId}\" (\"{command}\")."
            );
        }

        throw new InvalidOperationException($"Unsupported EventType: {eventType}.");
    }
}
