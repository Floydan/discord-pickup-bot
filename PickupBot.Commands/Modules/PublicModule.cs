using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using PickupBot.Commands.Extensions;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot.Commands.Modules
{
    [Name("Misc")]
    [Summary("Misc. commands")]
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;
        private readonly ISubscriberActivitiesRepository _activitiesRepository;

        public PublicModule(
            CommandService commandService,
            ISubscriberActivitiesRepository activitiesRepository)
        {
            _commandService = commandService;
            _activitiesRepository = activitiesRepository;
        }

        [Command("ping")]
        [Alias("pong", "hello")]
        public Task PingAsync()
            => ReplyAsync("pong!");

        [Command("help"), Alias("assist"), Summary("Shows help menu.")]
        public async Task Help([Remainder] string command = null)
        {
            const string botPrefix = "!";
            var helpEmbed = _commandService.GetDefaultHelpEmbed(command, botPrefix);
            await Context.Channel.SendMessageAsync(embed: helpEmbed);
        }

        [Command("version")]
        [Summary("Displays the currently deployed version of the bot")]
        public async Task Version()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            await ReplyAsync($"**Version:** `{version}`");
        }

        [Command("top10")]
        [Summary("Displays the currently top 10 of active players")]
        public async Task Top10()
        {
            var list = await _activitiesRepository.List(Context.Guild.Id);
            var activities = list as SubscriberActivities[] ?? list.ToArray();
            if (activities.IsNullOrEmpty())
            {
                await ReplyAsync("No data yet, get active!");
                return;
            }

            var users = Context.Guild.Users
                .Where(w => activities.Select(x => Convert.ToUInt64(x.RowKey)).Contains(w.Id))
                .ToList();

            var top10Create = activities.Where(w => w.PickupCreate > 0).OrderByDescending(w => w.PickupCreate).Take(10).ToList();
            var top10Add = activities.Where(w => w.PickupAdd > 0).OrderByDescending(w => w.PickupAdd).Take(10).ToList();
            var top10Promote = activities.Where(w => w.PickupPromote > 0).OrderByDescending(w => w.PickupPromote).Take(10).ToList();

            var sb = new StringBuilder();
            var counter = 0;
            if (top10Create.Any())
            {
                sb.AppendLine("**Top 10 pickup !create**");
                foreach (var c in top10Create)
                {
                    counter++;
                    var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                    if(user == null) continue;
                    sb.AppendLine($"{counter}. {user.Nickname ?? user.Username} - {c.PickupCreate} create(s)");
                }

                sb.AppendLine("");
            }

            if (top10Add.Any())
            {
                sb.AppendLine("**Top 10 pickup !add**");
                counter = 0;
                foreach (var c in top10Add)
                {
                    counter++;
                    var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                    if(user == null) continue;
                    sb.AppendLine($"{counter}. {user.Nickname ?? user.Username} - {c.PickupAdd} add(s)");
                }

                sb.AppendLine("");
            }

            if (top10Promote.Any())
            {
                sb.AppendLine("**Top 10 pickup spammers (!promote)**");

                counter = 0;
                foreach (var c in top10Promote)
                {
                    counter++;
                    var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                    if(user == null) continue;
                    sb.AppendLine($"{counter}. {user.Nickname ?? user.Username} - {c.PickupPromote} promote(s)");
                }
            }

            await ReplyAsync(sb.ToString());
        }
    }
}
