using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PickupBot.Commands.Modules
{
    [Group("admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        [Command("flag")]
        public async Task FlagUser(IGuildUser user, [Summary("Optional reason text"), Remainder] string reason)
        {
            if(user == null) return;
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync(
                $"User {user.Mention} has been flagged by {Context.User.Mention} and can no longer subscribe to pickup queues {Environment.NewLine} - _{reason}_");
        }

        [Command("unflag")]
        public async Task UnFlagUser(IGuildUser user)
        {
            if(user == null) return;
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync($"User {user.Mention} has been un-flagged by {Context.User.Mention} and can now subscribe to pickup queues");
        }
    }
}
