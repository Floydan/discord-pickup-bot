using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Commands.Models;
using PickupBot.Commands.Utilities;

// ReSharper disable MemberCanBePrivate.Global
namespace PickupBot.Commands.Modules
{
    public partial class PickupModule
    {
        [Command("servers")]
        [Alias("server", "ip")]
        [Summary("Returns a list of server addresses.")]
        public async Task Servers()
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
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
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

            try
            {
                var response = await RCON.UDPSendCommand("status", _rconHost, _rconPassword, _rconPort);
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
                Console.WriteLine(e);
            }
        }

        [Command("clientinfo")]
        public async Task ClientInfo(string player)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

            var clientInfo = new ClientInfo(await RCON.UDPSendCommand($"dumpuser {player}", _rconHost, _rconPassword, _rconPort));

            await ReplyAsync(
                $"```{Environment.NewLine}" +
                $"{clientInfo.ToTable()}" +
                $"{Environment.NewLine}```");
        }
    }
}
