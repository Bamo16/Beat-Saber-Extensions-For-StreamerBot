using BeatSaberExtensions.Extensions.BaseUserInfoExtensions;
using BeatSaberExtensions.Extensions.ComparableExtensions;
using BeatSaberExtensions.Extensions.InlineInvokeProxyExtensions;
using BeatSaberExtensions.Utility.Arguments;
using Streamer.bot.Plugin.Interface;
using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Utility.Logging;

public static class LogInspector
{
    private const string CommandSyntax =
        "!bsrlogs <show[error] [index] | add[user] <user> | remove[user] <user>>";

    public static string HandleLogsCommand(this ActionContext context) =>
        (context.Input0 ?? string.Empty).ToLowerInvariant() switch
        {
            "show" or "showerror" => context.HandleShowError(),

            "add" or "adduser" => context.HandleAddRemoveUser(true),

            "remove" or "removeuser" => context.HandleAddRemoveUser(false),

            { Length: > 0 } cmd => $"Invalid subcommand: \"{cmd}\". Syntax: {CommandSyntax}",

            _ => $"You must provide a subcommand. Syntax: {CommandSyntax}",
        };

    private static string HandleShowError(this ActionContext context)
    {
        if (UserConfig.RecentErrorMessages is not { Count: var count and > 0 } recent)
        {
            return "There are not any recent exceptions from Beat Saber Extensions.";
        }

        var index = context.GetErrorIndex(count);

        return string.Format(
            "{0}: {1}",
            index is 0 ? "Last Exception" : $"Exception {index}",
            recent[index]
        );
    }

    private static string HandleAddRemoveUser(this ActionContext context, bool shouldBelong)
    {
        if (
            context is { CallerTwitch: { IsModerator: false } caller }
            && !context.CPH.CheckGroupMembership(caller, UserConfig.LogUsersGroup)
        )
        {
            return $"Only moderators or members of the {UserConfig.LogUsersGroup} group can use this command.";
        }

        if (!context.TryGet("input1", out string lookup) || string.IsNullOrEmpty(lookup))
        {
            return $"You must provide a user to add or remove. Syntax: {CommandSyntax}";
        }

        if (context.CPH.GetUser<TwitchUserInfo>(lookup) is not { } user)
        {
            return $"Invalid user: \"{lookup}\".";
        }

        return user is { IsModerator: true }
            ? $"{user.Format()} is a moderator. Moderators {(shouldBelong ? "already" : "always")} have access."
            : EnsureLogGroupMembershipForUser(context.CPH, user, shouldBelong);
    }

    private static string EnsureLogGroupMembershipForUser(
        this IInlineInvokeProxy cph,
        BaseUserInfo user,
        bool shouldBelong
    )
    {
        if (cph.CheckGroupMembership(user, UserConfig.LogUsersGroup) == shouldBelong)
        {
            return $"{user.Format()} {(shouldBelong ? "is already in" : "is not in")} the {UserConfig.LogUsersGroup} group.";
        }

        var (action, preposition) = shouldBelong ? ("add", "to") : ("remove", "from");

        return cph.EnsureGroupMembershipForUser(
            user,
            UserConfig.LogUsersGroup,
            shouldBelong,
            alwaysLog: true
        )
            ? $"{user.Format()} {action}ed {preposition} the {UserConfig.LogUsersGroup} group."
            : $"Error trying to {action} {user.Format()} {preposition} the {UserConfig.LogUsersGroup} group.";
    }

    private static int GetErrorIndex(this ActionContext context, int recentCount) =>
        (
            context.TryGet("input1", out string input1) && int.TryParse(input1, out var i) ? i : 0
        ).Clamp(0, recentCount - 1);
}
