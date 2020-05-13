using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.CommandsExtension;
using Discord.Commands;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Utilities;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
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

        public PublicModule(
            CommandService commandService,
            ISubscriberActivitiesRepository activitiesRepository,
            GitHubService gitHubService)
        {
            _commandService = commandService;
            _activitiesRepository = activitiesRepository;
            _githubService = gitHubService;
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

            var top10Create = activities.Where(w => w.PickupCreate > 0)
                .OrderByDescending(w => w.PickupCreate)
                .Take(10)
                .ToList();

            var top10Add = activities.Where(w => w.PickupAdd > 0)
                .OrderByDescending(w => w.PickupAdd)
                .Take(10)
                .ToList();

            var top10Promote = activities.Where(w => w.PickupPromote > 0)
                .OrderByDescending(w => w.PickupPromote)
                .Take(10)
                .ToList();

            var sb = new StringBuilder();
            AddTopPlayers(sb, users, top10Create, "create");
            AddTopPlayers(sb, users, top10Add, "add");
            AddTopPlayers(sb, users, top10Promote, "promote", "spammers");

            await ReplyAsync(sb.ToString()).AutoRemoveMessage();
        }

        private static void AddTopPlayers(
            StringBuilder sb, 
            ICollection<SocketGuildUser> users, 
            ICollection<SubscriberActivities> activities, 
            string type, 
            string headlineAdditions = "")
        {
            if (!activities.Any()) return;

            var counter = 0;
            sb.AppendLine($"**Top 10{(string.IsNullOrEmpty(headlineAdditions) ? " " : $" {headlineAdditions}")} !{type}**");
            foreach (var c in activities)
            {
                counter++;
                var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                if (user == null) continue;
                sb.AppendLine($"{counter}. {user.Nickname ?? user.Username} - {c.PickupCreate} {type.Pluralize(c.PickupPromote)}");
            }

            sb.AppendLine("");
        }

        [Command("releases")]
        [Summary("Retrieves the 3 latest releases from github")]
        public async Task Releases()
        {
            using (Context.Channel.EnterTypingState())
            {
                var releases = await _githubService.GetReleases();

                var gitHubReleases = releases as GitHubRelease[] ?? releases.ToArray();

                foreach (var release in gitHubReleases.Take(3))
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
    }
}
