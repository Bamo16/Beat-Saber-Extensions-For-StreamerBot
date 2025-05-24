using System;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.Logging;
using Streamer.bot.Plugin.Interface;

public class CPHInline : CPHInlineBase // Remove " : CPHInlineBase" when pasting code into streamer.bot
{
    private const string ActionName = "Beat Saber Extensions";

    private StreamerBotLogger _logger;
    private StreamerBotEventHandler _sbEventHandler;

    public void Init()
    {
        try
        {
            _logger = new StreamerBotLogger(CPH, ActionName);
            UserConfig.Init(_logger);
            _sbEventHandler = new StreamerBotEventHandler(CPH, _logger);

            _logger.Log("Completed successfully.");
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                _logger.HandleException(ex, setArgument: false);
            else
                CPH.HandleNullLoggerOnInit(ex, nameof(_logger), ActionName);
        }
    }

    public void Dispose() => _sbEventHandler?.Dispose();

    public bool Execute()
    {
        _logger.LogActionStart(args, out var executeSuccess, out var sbArgs, out var eventType);

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
            _logger.HandleException(ex);
        }
        finally
        {
            _logger.LogActionCompletion(executeSuccess);
        }

        return executeSuccess;
    }
}
