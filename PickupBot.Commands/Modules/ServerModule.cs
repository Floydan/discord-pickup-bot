using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using PickupBot.Commands.Infrastructure;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;
using PickupBot.Encryption;

namespace PickupBot.Commands.Modules
{
    [Name("Server admin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [Summary("Not fully implemented yet")]
    public class ServerModule : InteractiveBase<SocketCommandContext>
    {
        private readonly IServerRepository _serverRepository;
        private readonly EncryptionSettings _encryptionSettings;

        public ServerModule(IServerRepository serverRepository, EncryptionSettings encryptionSettings)
        {
            _serverRepository = serverRepository;
            _encryptionSettings = encryptionSettings;
        }

        [Command("addserver")]
        [Alias("serveradd")]
        [Summary("Add a server; If you add a server with rcon password make sure you delete the message with the password since it is visible for everyone in the channel.")]
        public async Task AddServer(
            [Name("Server address:server port")]string fullAddress,
            [Remainder, Name("Optional RCon password")]string rconPassword = default)
        {
            using (Context.Channel.EnterTypingState())
            {
                var parts = fullAddress.Split(':', StringSplitOptions.RemoveEmptyEntries);
                var host = parts.First();
                var port = 0;
                if (parts.Length > 1)
                    int.TryParse(parts.Last(), out port);

                var client = new HttpClient
                {
                    BaseAddress = new Uri("http://ip-api.com/")
                };
                var response = await client.GetStringAsync($"/json/{host}?fields=36749595");

                var server = JsonConvert.DeserializeObject<Server>(response);
                server.Host = host.ToLowerInvariant();
                server.Port = port;
                server.PartitionKey = Context.Guild.Id.ToString();
                server.RowKey = host.ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(rconPassword))
                {
                    server.RconPassword =
                        EncryptionProvider.AESEncrypt(rconPassword, _encryptionSettings.Key, _encryptionSettings.IV);
                }

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

        [Command("setrconpassword")]
        [Alias("setrconpass", "setrconpassw")]
        public async Task SetRconPassword(string host)
        {
            var server = await _serverRepository.Find(Context.Guild.Id, host);
            if (server != null)
            {
                await ReplyAndDeleteAsync($"Check your DM's for information on how to set the password {Context.User.Mention}",
                    timeout: TimeSpan.FromSeconds(10));

                var msg = await Context.User.SendMessageAsync($"Type in the rcon password you wish to set for the `{server.Host}` server");

                var response = await NextMessageAsync(new EnsureFromUserDmCriterion(msg.Channel, Context.User), TimeSpan.FromSeconds(30));

                if (!string.IsNullOrWhiteSpace(response?.Content))
                {
                    server.RconPassword = EncryptionProvider.AESEncrypt(response.Content, _encryptionSettings.Key, _encryptionSettings.IV);
                    var result = await _serverRepository.Save(server);
                    if (result)
                    {
                        await Context.User.SendMessageAsync($"Rcon password for server `{server.Host}` has been saved");
                        return;
                    }
                }

                await Context.User.SendMessageAsync($"Failed to save the password for the `{server.Host}` server");
            }
            else
                await ReplyAndDeleteAsync($"Could not find a server with host `{host}`", timeout: TimeSpan.FromSeconds(10));
        }
    }
}
