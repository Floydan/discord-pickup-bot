using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Commands.Repositories;

namespace PickupBot.Commands.Modules
{
    [Group("Admin"), Name("Admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Not fully implemented yet")]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly IQueueRepository _queueRepository;

        public AdminModule(IQueueRepository queueRepository)
        {
            _queueRepository = queueRepository;
        }

        [Command("flag")]
        [Summary("Flags a user so they can't enter queues")]
        public async Task FlagUser([Summary("User to flag")]IGuildUser user, [Summary("Optional reason text"), Remainder] string reason)
        {
            if(user == null) return;

            await _queueRepository.FlagUser(user, Context.Guild.Id);
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync(
                $"`User {user.Mention} has been flagged by {Context.User.Mention} and can no longer subscribe to pickup queues {Environment.NewLine} {(!string.IsNullOrEmpty(reason) ? $"_{reason}_" : "")}`");
        }

        [Command("unflag")]
        [Summary("Un-flags a user so they can enter queues")]
        public async Task UnFlagUser([Summary("User to un-flag")]IGuildUser user)
        {
            if(user == null) return;

            await _queueRepository.UnFlagUser(user, Context.Guild.Id);
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync($"`User {user.Mention} has been un-flagged by {Context.User.Mention} and can now subscribe to pickup queues`");
        }

        [Command("list")]
        [Summary("List all flagged users")]
        public async Task GetAll()
        {
            var flaggedUsers = await _queueRepository.GetAllFlaggedUsers(Context.Guild.Id);
            
            //flag user so they can't be added to pickup queues
            await ReplyAsync($"`**Flagged users:**{Environment.NewLine}{string.Join(", ", flaggedUsers.Select((u, i) => $"{i}. {u.Name}`"))}");
        }
    }
}
