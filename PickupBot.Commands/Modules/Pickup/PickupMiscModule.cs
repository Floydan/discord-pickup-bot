using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Commands.Models;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;
using PickupBot.Encryption;

// ReSharper disable MemberCanBePrivate.Global
namespace PickupBot.Commands.Modules.Pickup
{
    [Name("Pickup misc. actions")]
    [Summary("Commands for handling pickups - misc. actions")]
    public class PickupMiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly EncryptionSettings _encryptionSettings;
        private readonly ILogger<PickupMiscModule> _logger;
        private readonly IServerRepository _serverRepository;
        private readonly string _rconPassword;
        private readonly string _rconHost;
        private readonly int _rconPort;

        public PickupMiscModule(PickupBotSettings pickupBotSettings, EncryptionSettings encryptionSettings, ILogger<PickupMiscModule> logger, IServerRepository serverRepository)
        {
            _encryptionSettings = encryptionSettings;
            _logger = logger;
            _serverRepository = serverRepository;

            _rconPassword = pickupBotSettings.RCONServerPassword ?? "";
            _rconHost = pickupBotSettings.RCONHost ?? "";
            int.TryParse(pickupBotSettings.RCONPort ?? "0", out _rconPort);
        }

        [Command("serverstatus")]
        public async Task ServerStatus(string host = "")
        {
            if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

            var rconPassword = _rconPassword;
            var rconPort = _rconPort;
            var rconHost = _rconHost;
            if (!string.IsNullOrEmpty(host))
            {
                var server = await _serverRepository.Find(Context.Guild.Id, host);
                if (server != null)
                {
                    rconPort = server.Port;
                    rconHost = server.Host;
                    rconPassword = !string.IsNullOrWhiteSpace(server.RconPassword) ? 
                        EncryptionProvider.AESDecrypt(server.RconPassword, _encryptionSettings.Key, _encryptionSettings.IV) : 
                        string.Empty;

                    if (string.IsNullOrWhiteSpace(rconPassword) || string.IsNullOrWhiteSpace(rconHost) || rconPort == 0)
                    {
                        await Context.Message.ReplyAsync(
                            $"Can't show server status since no rcon password has been set for the server {host}")
                            .AutoRemoveMessage(15);
                        return;
                    }
                }
            }

            try
            {
                var response = await RCON.UDPSendCommand("status", rconHost, rconPassword, rconPort).ConfigureAwait(false);

                _logger.LogInformation($"serverstatus response: {response}");
                var serverStatus = new ServerStatus(response);

                var embed = new EmbedBuilder
                {
                    Title = $"Server status for {rconHost}",
                    Description = $"**Map:** _{serverStatus.Map} _" +
                                  $"{Environment.NewLine}" +
                                  "**Players**" +
                                  $"{Environment.NewLine}"
                };

                embed.Description += $"```{Environment.NewLine}" +
                                     (serverStatus.Players.Any() ?
                                        $"{serverStatus.PlayersToTable()}" :
                                        "No players are currently online") +
                                     $"{Environment.NewLine}```";

                await Context.Message.ReplyAsync(embed: embed.Build());
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }

        [Command("clientinfo")]
        public async Task ClientInfo(string player)
        {
            _logger.LogInformation("clientinfo called");
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

            var userdata = await RCON.UDPSendCommand($"dumpuser {player}", _rconHost, _rconPassword, _rconPort).ConfigureAwait(false);
            if (userdata.IndexOf("is not on the server", StringComparison.OrdinalIgnoreCase) != -1)
            {
                await Context.Message.ReplyAsync(
                    $"```{Environment.NewLine}" +
                    $"{userdata}" +
                    $"{Environment.NewLine}```");
                return;
            }

            var clientInfo = new ClientInfo(userdata);

            await Context.Message.ReplyAsync(
                $"```{Environment.NewLine}" +
                $"{clientInfo.ToTable()}" +
                $"{Environment.NewLine}```");
        }
    }
}
