using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Data.Repositories;

namespace PickupBot.Commands.Modules
{
    [Name("Admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Not fully implemented yet")]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly IFlaggedSubscribersRepository _flagRepository;

        public AdminModule(IFlaggedSubscribersRepository flagRepository)
        {
            _flagRepository = flagRepository;
        }

        [Command("flag")]
        [Alias("ban")]
        [Summary("Flags a user so they can't enter queues")]
        public async Task FlagUser([Summary("User to flag")]IGuildUser user, [Summary("Optional reason text"), Remainder] string reason)
        {
            if(user == null) return;

            await _flagRepository.Flag(user, reason);
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync(
                $"User {user.Mention} has been flagged by {Context.User.Mention} and can no longer subscribe to pickup queues {Environment.NewLine} {(!string.IsNullOrEmpty(reason) ? $" - _{reason}_" : "")}");
        }

        [Command("unflag")]
        [Alias("unban")]
        [Summary("Un-flags a user so they can enter queues")]
        public async Task UnFlagUser([Summary("User to un-flag")]IGuildUser user)
        {
            if(user == null) return;

            await _flagRepository.UnFlag(user);
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync($"User {user.Mention} has been un-flagged by {Context.User.Mention} and can now subscribe to pickup queues");
        }

        [Command("flaglist")]
        [Alias("banlist")]
        [Summary("List all flagged users")]
        public async Task GetAll()
        {
            var flaggedUsers = await _flagRepository.List(Context.Guild.Id.ToString());
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync($"**Flagged users:**{Environment.NewLine}{string.Join(", ", flaggedUsers.Select((u, i) => $"{i+1}. {u.Name}`"))}");
        }
    }
}
