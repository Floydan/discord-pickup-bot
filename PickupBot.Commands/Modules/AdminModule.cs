using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;
using PickupBot.Encryption;

namespace PickupBot.Commands.Modules
{
    [Name("Admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly IFlaggedSubscribersRepository _flagRepository;
        private readonly IServerRepository _serverRepository;
        private readonly EncryptionSettings _encryptionSettings;

        public AdminModule(IFlaggedSubscribersRepository flagRepository, IServerRepository serverRepository, EncryptionSettings encryptionSettings)
        {
            _flagRepository = flagRepository;
            _serverRepository = serverRepository;
            _encryptionSettings = encryptionSettings;
        }

        [Command("flag")]
        [Alias("ban")]
        [Summary("Flags a user so they can't enter queues")]
        public async Task FlagUser([Summary("User to flag")]IGuildUser user, [Summary("Optional reason text"), Remainder] string reason)
        {
            if (user == null) return;

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
            if (user == null) return;

            await _flagRepository.UnFlag(user);
            BotMessageHelper.AutoRemoveMessage(
                //flag user so they can't be added to pickup queues
                await ReplyAsync($"User {user.Mention} has been un-flagged by {Context.User.Mention} and can now subscribe to pickup queues")
            );
        }

        [Command("flaglist")]
        [Alias("banlist")]
        [Summary("List all flagged users")]
        public async Task GetAll()
        {
            var flaggedUsers = await _flagRepository.List(Context.Guild.Id.ToString());

            BotMessageHelper.AutoRemoveMessage(
                //flag user so they can't be added to pickup queues
                await ReplyAsync($"**Flagged users:**{Environment.NewLine}{string.Join(", ", flaggedUsers.Select((u, i) => $"{i + 1}. {u.Name}`"))}")
            );
        }
    }
}
