using Streamer.bot.Plugin.Interface.Model;

namespace BeatSaberExtensions.Extensions.GroupUserExtensions;

public static class GroupUserExtensions
{
    public static BaseUserInfo ToBaseUserInfo(this GroupUser user) =>
        new BaseUserInfo()
        {
            UserId = user.Id,
            UserLogin = user.Login,
            UserName = user.Username,
        };
}
