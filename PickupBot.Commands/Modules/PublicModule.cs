using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;
using PickupBot.GitHub;
using PickupBot.GitHub.Models;

namespace PickupBot.Commands.Modules
{
    [Name("Misc")]
    [Summary("Misc. commands")]
    public class PublicModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;
        private readonly ISubscriberActivitiesRepository _activitiesRepository;
        private readonly GitHubService _githubService;
        private readonly IServerRepository _serverRepository;

        public PublicModule(
            CommandService commandService,
            ISubscriberActivitiesRepository activitiesRepository,
            GitHubService gitHubService,
            IServerRepository serverRepository)
        {
            _commandService = commandService;
            _activitiesRepository = activitiesRepository;
            _githubService = gitHubService;
            _serverRepository = serverRepository;
        }

        [Command("ping")]
        [Alias("pong", "hello")]
        public async Task PingAsync() => await ReplyAsync("pong!").AutoRemoveMessage();

        [Command("help"), Alias("assist", "commands"), Summary("Shows help menu via a Direct Message (DM).")]
        public async Task Help([Remainder] string command = null)
        {
            const string botPrefix = "!";
            var helpEmbed = _commandService.GetDefaultHelpEmbed(command, botPrefix);
            await Context.User.SendMessageAsync(embed: helpEmbed);
            await ReplyAsync($"Check your DM's {Context.User.Mention}").AutoRemoveMessage(10);
        }

        [Command("version")]
        [Summary("Displays the currently deployed version of the bot")]
        public async Task Version()
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            BotMessageHelper.AutoRemoveMessage(await ReplyAsync($"**Version:** `{version}`"), 10);
        }

        [Command("top10")]
        [Summary("Displays the currently top 10 of active players")]
        public async Task Top10()
        {
            var activity = await _activitiesRepository.Find((IGuildUser)Context.User);
            activity.PickupTop10 += 1;
            await _activitiesRepository.Update(activity);

            var list = await _activitiesRepository.List(Context.Guild.Id);
            var activities = list as SubscriberActivities[] ?? list.ToArray();
            if (activities.IsNullOrEmpty())
            {
                BotMessageHelper.AutoRemoveMessage(await ReplyAsync("No data yet, get active!"), 10);
                return;
            }

            var users = Context.Guild.Users
                .Where(w => activities.Select(x => Convert.ToUInt64(x.RowKey)).Contains(w.Id))
                .ToList();

            var embed = new EmbedBuilder
            {
                Title = "Top 10"
            };

            AddTopPlayers(embed, users, activities, a => a.PickupCreate, "create");
            AddTopPlayers(embed, users, activities, a => a.PickupAdd, "add");
            AddTopPlayers(embed, users, activities, a => a.PickupPromote, "promote", "spammers");
            AddTopPlayers(embed, users, activities, a => a.PickupTop10, "top10", "stats junkies");

            await ReplyAsync(embed: embed.Build()).AutoRemoveMessage(60);
        }

        private static void AddTopPlayers(
            EmbedBuilder embed,
            ICollection<SocketGuildUser> users, 
            ICollection<SubscriberActivities> activities, 
            Func<SubscriberActivities, int> keySelector,
            string type, 
            string headlineAdditions = "")
        {
            if (!activities.Any()) return;

            var top10 = activities.Where(w => keySelector.Invoke(w) > 0)
                .OrderByDescending(keySelector)
                .Take(10)
                .ToList();
            
            if (!top10.Any()) return;

            var counter = 0;
            
            var sb = new StringBuilder();

            foreach (var c in top10)
            {
                counter++;
                var badge = counter == 1 ? ":first_place:" : counter == 2 ? ":second_place:" : counter == 3 ? ":third_place:" : "";
                var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                if (user == null) continue;
                var val = keySelector.Invoke(c);
                sb.AppendLine($"{badge} {user.Nickname ?? user.Username} _({val})_");
            }

            embed.WithFields(new EmbedFieldBuilder
            {
                IsInline = true, 
                Name = $"**Top 10 `!{type}` {headlineAdditions}**", 
                Value = sb.ToString()
            });
        }

        [Command("releases")]
        [Summary("Retrieves the 2 latest releases from github")]
        public async Task Releases()
        {
            using (Context.Channel.EnterTypingState())
            {
                var releases = await _githubService.GetReleases();

                var gitHubReleases = releases as GitHubRelease[] ?? releases.ToArray();

                foreach (var release in gitHubReleases.Take(2))
                {
                    var embed = new EmbedBuilder
                    {
                        Author = new EmbedAuthorBuilder()
                            .WithIconUrl(release.Author?.AvatarUrl)
                            .WithName(release.Author?.Name),
                        Title = release.Name,
                        Description = release.Body,
                        Url = release.Url

                    }.Build();
                    
                    await ReplyAsync(embed:embed).AutoRemoveMessage();
                }
            }
        }

        [Command("servers")]
        [Alias("server", "ip")]
        [Summary("Returns a list of server addresses.")]
        public async Task Servers()
        {
            using (Context.Channel.EnterTypingState())
            {
                var servers = await _serverRepository.List(Context.Guild.Id);

                var sb = new StringBuilder();
                if (servers.IsNullOrEmpty())
                {
                    sb.AppendLine("No servers have been added yet.");
                }
                else
                {
                    var continents = servers.Select(s => s.Continent).OrderBy(c => c).Distinct();
                    foreach (var continent in continents)
                    {
                        sb.AppendLine($"**{continent}**")
                          .AppendLine("```markdown")
                          .AppendLine(AsciiTableGenerator.CreateAsciiTableFromDataTable(
                            ContinentToTable(servers.Where(s => s.Continent == continent)
                                .OrderBy(s => s.Country)
                                .ThenBy(s => s.City))
                            )?.ToString())
                          .AppendLine("```");
                    }
                }

                await ReplyAsync(embed: new EmbedBuilder
                {
                    Title = "Servers",
                    Description = sb.ToString(),
                    Color = Color.Green
                }.Build());
            }
        }

        private static DataTable ContinentToTable(IEnumerable<Server> servers)
        {
            var dataTable = new DataTable();
            dataTable.Columns.AddRange(new []
            {
                new DataColumn("Country"), 
                new DataColumn("Region"), 
                new DataColumn("City"), 
                new DataColumn("Host"), 
                new DataColumn("Port"), 
            });
            foreach (var server in servers)
            {
                var row = dataTable.NewRow();
                row[0] = server.Country;
                row[1] = server.RegionName;
                row[2] = server.City;
                row[3] = server.Host;
                row[4] = server.Port > 0 ? server.Port.ToString() : "";
                dataTable.Rows.Add(row);
            }

            return dataTable;
        }
    }
}
