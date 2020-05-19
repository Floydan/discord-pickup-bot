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

            var sb = new StringBuilder();
            AddTopPlayers(sb, users, activities, a => a.PickupCreate, "create");
            AddTopPlayers(sb, users, activities, a => a.PickupAdd, "add");
            AddTopPlayers(sb, users, activities, a => a.PickupPromote, "promote", "spammers");
            AddTopPlayers(sb, users, activities, a => a.PickupTop10, "top10", "stats junkies");

            await ReplyAsync(sb.ToString()).AutoRemoveMessage();
        }

        private static void AddTopPlayers(
            StringBuilder sb, 
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
            sb.AppendLine($"**Top 10 `!{type}` {headlineAdditions}**");

            foreach (var c in top10)
            {
                counter++;
                var user = users.FirstOrDefault(u => u.Id == Convert.ToUInt64(c.RowKey));
                if (user == null) continue;
                var val = keySelector.Invoke(c);
                sb.AppendLine($"{counter}. {user.Nickname ?? user.Username} - {val} {type.Pluralize(val)}");
            }

            sb.AppendLine("");
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
    }
}
