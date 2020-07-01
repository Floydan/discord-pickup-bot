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

namespace PickupBot.Commands.Modules
{
    [Name("Admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Not fully implemented yet")]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly IFlaggedSubscribersRepository _flagRepository;
        private readonly IServerRepository _serverRepository;

        public AdminModule(IFlaggedSubscribersRepository flagRepository, IServerRepository serverRepository)
        {
            _flagRepository = flagRepository;
            _serverRepository = serverRepository;
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

        [Command("addserver")]
        [Alias("serveradd")]
        [Summary("Add a server")]
        public async Task AddServer(string host, [Remainder]string port = default)
        {
            using (Context.Channel.EnterTypingState())
            {
                int.TryParse(port, out var iPort);

                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://ip-api.com/")
                };
                var response = await client.GetStringAsync($"/json/{host}?fields=36749595");

                var server = JsonConvert.DeserializeObject<Server>(response);
                server.Host = host.ToLowerInvariant();
                server.Port = iPort;
                server.PartitionKey = Context.Guild.Id.ToString();
                server.RowKey = host.ToLowerInvariant();

                var result = await _serverRepository.Save(server);

                if (result)
                {
                    await ReplyAsync("Server added");
                }
                else
                {
                    await ReplyAsync("Failed to add server.");
                }
            }
        }

        [Command("deleteserver")]
        [Alias("serverdelete", "removeserver", "serverremove")]
        [Summary("Remove a server")]
        public async Task RemoveServer(string host)
        {
            using (Context.Channel.EnterTypingState())
            {
                var result = await _serverRepository.Delete(Context.Guild.Id, host);

                if (result)
                {
                    await ReplyAsync("Server deleted");
                }
                else
                {
                    await ReplyAsync("Failed to delete server.");
                }
            }
        }
    }
}
