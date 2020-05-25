using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Commands.Models;
using PickupBot.Data.Models;

// ReSharper disable MemberCanBePrivate.Global
namespace PickupBot.Commands.Modules
{
    [Name("Pickup misc. actions")]
    [Summary("Commands for handling pickups - misc. actions")]
    public class PickupMiscModule : ModuleBase<SocketCommandContext>
    {
        private readonly ILogger<PickupMiscModule> _logger;
        private readonly string _rconPassword;
        private readonly string _rconHost;
        private readonly int _rconPort;

        public PickupMiscModule(PickupBotSettings pickupBotSettings, ILogger<PickupMiscModule> logger)
        {
            _logger = logger;

            _rconPassword = pickupBotSettings.RCONServerPassword ?? "";
            _rconHost = pickupBotSettings.RCONHost ?? "";
            int.TryParse(pickupBotSettings.RCONPort ?? "0", out _rconPort);
        }

        [Command("servers")]
        [Alias("server", "ip")]
        [Summary("Returns a list of server addresses.")]
        public async Task Servers()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var sb = new StringBuilder()
                .AppendLine("**Scandinavia**")
                .AppendLine("ra3.se")
                .AppendLine("pickup.ra3.se")
                .AppendLine("")
                .AppendLine("**US West**")
                .AppendLine("70.190.244.70:27950");

            await ReplyAsync(embed: new EmbedBuilder
            {
                Title = "Server addresses",
                Description = sb.ToString(),
                Color = Color.Green
            }.Build());
        }

        [Command("serverstatus")]
        public async Task ServerStatus()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

            try
            {
                var response = await RCON.UDPSendCommand("status", _rconHost, _rconPassword, _rconPort).ConfigureAwait(false);
                
                _logger.LogInformation($"serverstatus response: {response}");
                var serverStatus = new ServerStatus(response);

                var embed = new EmbedBuilder
                {
                    Title = $"Server status for {_rconHost}",
                    Description = $"**Map:** _{serverStatus.Map} _" +
                                  $"{Environment.NewLine}" +
                                  "**Players**" +
                                  $"{Environment.NewLine}"
                };

                if (serverStatus.Players.Any())
                {
                    embed.Description += $"```{Environment.NewLine}" +
                                         $"{serverStatus.PlayersToTable()}" +
                                         $"{Environment.NewLine}```";
                }
                else
                {
                    embed.Description += $"```{Environment.NewLine}" +
                                         "No players are currently online" +
                                         $"{Environment.NewLine}```";
                }

                await ReplyAsync(embed: embed.Build());
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
            if(userdata.IndexOf("is not on the server", StringComparison.OrdinalIgnoreCase) != -1)
            {
                await ReplyAsync(
                    $"```{Environment.NewLine}" +
                    $"{userdata}" +
                    $"{Environment.NewLine}```");
                return;
            }

            var clientInfo = new ClientInfo(userdata);

            await ReplyAsync(
                $"```{Environment.NewLine}" +
                $"{clientInfo.ToTable()}" +
                $"{Environment.NewLine}```");
        }
    }
}
