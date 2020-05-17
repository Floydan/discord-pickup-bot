using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Utilities;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
// ReSharper disable MemberCanBePrivate.Global

namespace PickupBot.Commands.Modules
{
    [Name("Pickup")]
    [Summary("Commands for handling pickup queues")]
    public partial class PickupModule : ModuleBase<SocketCommandContext>
    {
        private readonly IQueueRepository _queueRepository;
        private readonly IFlaggedSubscribersRepository _flagRepository;
        private readonly ISubscriberActivitiesRepository _activitiesRepository;
        private readonly string _rconPassword;
        private readonly string _rconHost;
        private readonly int _rconPort;

        public PickupModule(
            IQueueRepository queueRepository,
            IFlaggedSubscribersRepository flagRepository,
            ISubscriberActivitiesRepository activitiesRepository,
            PickupBotSettings pickupBotSettings)
        {
            _queueRepository = queueRepository;
            _flagRepository = flagRepository;
            _activitiesRepository = activitiesRepository;
            _rconPassword = pickupBotSettings.RCONServerPassword ?? "";
            _rconHost = pickupBotSettings.RCONHost ?? "";
            int.TryParse(pickupBotSettings.RCONPort ?? "0", out _rconPort);
        }

        [Command("create")]
        [Summary("Creates a pickup queue")]
        public async Task Create(
            [Name("Queue name")] string queueName,
            [Name("Team size")]
            int? teamSize = null,
            [Remainder, Name("Operator flags")] string operators = "")
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (!teamSize.HasValue)
                teamSize = 4;

            if (teamSize > 16)
                teamSize = 16;

            if (!await VerifyUserFlaggedStatus())
                return;

            var ops = OperatorParser.Parse(operators);

            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());

            if (queue != null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' already exists!`").AutoRemoveMessage(10);
                return;
            }

            var activity = await _activitiesRepository.Find((IGuildUser)Context.User);
            activity.PickupCreate += 1;
            await _activitiesRepository.Update(activity);

            var rconEnabled = ops?.ContainsKey("-rcon") ?? true;
            if (ops?.ContainsKey("-norcon") == true)
                rconEnabled = false;

            await _queueRepository.AddQueue(new PickupQueue(Context.Guild.Id.ToString(), queueName)
            {
                Name = queueName,
                GuildId = Context.Guild.Id.ToString(),
                OwnerName = GetNickname(Context.User),
                OwnerId = Context.User.Id.ToString(),
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                TeamSize = teamSize.Value,
                IsCoop = ops?.ContainsKey("-coop") ?? false,
                Rcon = rconEnabled,
                Subscribers = new List<Subscriber> { new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) } },
                Host = ops?.ContainsKey("-host") ?? false ? ops["-host"]?.FirstOrDefault() : null,
                Port = int.Parse((ops?.ContainsKey("-port") ?? false ? ops["-port"]?.FirstOrDefault() : null) ?? "0"),
                Games = ops?.ContainsKey("-game") ?? false ? ops["-game"] : Enumerable.Empty<string>(),
            });

            await Context.Channel.SendMessageAsync($"`Queue '{queueName}' was added by {GetNickname(Context.User)}`");
        }

        [Command("rename")]
        [Summary("Rename a queue")]
        public async Task Rename([Name("Queue name")] string queueName, [Name("New name")] string newName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await VerifyQueueByName(queueName);
            if (queue == null)
            {
                return;
            }

            var isAdmin = (Context.User as IGuildUser)?.GuildPermissions.Has(GuildPermission.Administrator) ?? false;
            if (isAdmin || queue.OwnerId == Context.User.Id.ToString())
            {
                var newQueueCheck = await _queueRepository.FindQueue(newName, Context.Guild.Id.ToString());
                if (newQueueCheck != null)
                {
                    await ReplyAsync($"`A queue with the name '{newName}' already exists.`").AutoRemoveMessage(10);
                    return;
                }

                var newQueue = new PickupQueue(Context.Guild.Id.ToString(), newName)
                {
                    OwnerId = queue.OwnerId,
                    OwnerName = queue.OwnerName,
                    Created = queue.Created,
                    Updated = DateTime.UtcNow,
                    TeamSize = queue.TeamSize,
                    Subscribers = queue.Subscribers,
                    WaitingList = queue.WaitingList,
                    IsCoop = queue.IsCoop,
                    Rcon = queue.Rcon,
                    Host = queue.Host,
                    Port = queue.Port,
                    Games = queue.Games
                };

                var result = await _queueRepository.AddQueue(newQueue);
                if (result)
                {
                    await _queueRepository.RemoveQueue(queue);
                    await ReplyAsync($"The queue '{queue.Name}' has been renamed to '{newQueue.Name}'");
                    await ReplyAsync($"`{newQueue.Name} - {ParseSubscribers(newQueue)}`");
                    return;
                }

                await ReplyAsync("An error occured when trying to update the queue name, try again.").AutoRemoveMessage(10);
                return;
            }

            await ReplyAsync("`You do not have permission to rename this queue, you have to be either the owner or a server admin`").AutoRemoveMessage(10);
        }

        [Command("delete")]
        [Alias("del", "cancel")]
        [Summary("If you are the creator of the queue you can use this to delete it")]
        public async Task Delete([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await VerifyQueueByName(queueName);
            if (queue == null)
            {
                return;
            }

            var isAdmin = (Context.User as IGuildUser)?.GuildPermissions.Has(GuildPermission.Administrator) ?? false;
            if (isAdmin || queue.OwnerId == Context.User.Id.ToString())
            {
                var result = await _queueRepository.RemoveQueue(queueName, Context.Guild.Id.ToString());
                var message = result ?
                    $"`Queue '{queueName}' has been canceled`" :
                    $"`Queue with the name '{queueName}' doesn't exists or you are not the owner of the queue!`";
                await Context.Channel.SendMessageAsync(message).AutoRemoveMessage(10);
                return;
            }

            await Context.Channel.SendMessageAsync("You do not have permission to remove the queue.").AutoRemoveMessage(10);
        }

        [Command("list")]
        [Summary("List all active queues")]
        public async Task List()
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            //find all active queues
            var queues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());
            Embed embed;
            //if queues found
            var pickupQueues = queues as PickupQueue[] ?? queues.ToArray();
            if (!pickupQueues.Any())
            {
                embed = new EmbedBuilder
                {
                    Title = "Active queues",
                    Description = "There are no active pickup queues at this time, maybe you should `!create` one \uD83D\uDE09",
                    Color = Color.Orange
                }.Build();

                await Context.Channel.SendMessageAsync(embed: embed).AutoRemoveMessage(10);
                return;
            }

            var ordered = pickupQueues.OrderByDescending(w => w.Readiness);
            var sb = new StringBuilder();
            foreach (var q in ordered)
            {
                sb.Clear()
                  .AppendLine($"`!add \"{q.Name}\"` to join!")
                  .AppendLine("")
                  .AppendLine($"Created by _{q.OwnerName}_ {(q.IsCoop ? "(_coop_)" : "")}")
                  .AppendLine("```")
                  .AppendLine($"[{q.Subscribers.Count}/{q.MaxInQueue}] - {ParseSubscribers(q)}")
                  .AppendLine("```");

                if (!q.WaitingList.IsNullOrEmpty())
                    sb.AppendLine($"In waitlist: **{q.WaitingList.Count}**");
                if (!q.Games.IsNullOrEmpty())
                    sb.AppendLine($"**Game(s): ** _{string.Join(", ", q.Games)}_");
                if (!string.IsNullOrWhiteSpace(q.Host))
                    sb.AppendLine($"**Server**: _{q.Host ?? "ra3.se"}:{(q.Port > 0 ? q.Port : 27960)}_");

                embed = new EmbedBuilder
                {
                    Title = $"{q.Name}{(q.Started ? " - Started" : "")}",
                    Description = sb.ToString(),
                    Color = Color.Orange
                }.Build();
                await Context.Channel.SendMessageAsync(embed: embed).AutoRemoveMessage();
            }
        }

        [Command("waitlist")]
        [Summary("Lists all the players in a given queues wait list")]
        public async Task WaitList([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());

            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
                return;
            }

            var waitlist = string.Join($"{Environment.NewLine} ", queue.WaitingList.Select((w, i) => $"{i + 1}: {w.Name}"));
            if (string.IsNullOrWhiteSpace(waitlist))
                waitlist = "No players in the waiting list";

            var embed = new EmbedBuilder
            {
                Title = $"Players in waiting list for queue {queue.Name}",
                Description = waitlist,
                Color = Color.Orange
            }.Build();
            await Context.Channel.SendMessageAsync(embed: embed).AutoRemoveMessage(15);
        }

        [Command("promote")]
        [Summary("Promotes one specific or all queues to the 'promote-role' role")]
        public async Task Promote([Name("Queue name"), Summary("Queue name"), Remainder] string queueName = "")
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var activity = await _activitiesRepository.Find((IGuildUser)Context.User);
            activity.PickupPromote += 1;
            await _activitiesRepository.Update(activity);

            PickupQueue queue = null;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());
                if (queue == null)
                {
                    await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
                    return;
                }

                if (queue.MaxInQueue <= queue.Subscribers.Count)
                {
                    await ReplyAsync("Queue is full, why the spam?").AutoRemoveMessage(10);
                    return;
                }
            }

            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote") ??
                       (IRole)await Context.Guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange, isHoisted: false, isMentionable: true);
            if (role == null)
                return; //Failed to get or create role;

            using (Context.Channel.EnterTypingState())
            {

                var users = Context.Guild.Users.Where(w => w.Roles.Any(r => r.Id == role.Id)).ToList();
                if (!users.Any())
                {
                    await ReplyAsync("No users have subscribed using the `!subscribe` command.").AutoRemoveMessage(10);
                    return;
                }

                if (string.IsNullOrWhiteSpace(queueName))
                {
                    var queues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());
                    var filtered = queues.Where(q => q.MaxInQueue > q.Subscribers.Count).ToArray();
                    if (filtered.Any())
                        await ReplyAsync($"There are {filtered.Length} pickup queues with spots left, check out the `!list`! - {role.Mention}").AutoRemoveMessage();
                }
                else if (queue != null)
                {
                    var sb = new StringBuilder()
                        .AppendLine("**Current queue**")
                        .AppendLine($"`{ParseSubscribers(queue)}`")
                        .AppendLine("")
                        .AppendLine($"**Spots left**: {queue.MaxInQueue - queue.Subscribers.Count}")
                        .AppendLine($"**Team size**: {queue.TeamSize}")
                        .AppendLine("")
                        .AppendLine($"Just run `!add \"{queue.Name}\"` in channel <#{Context.Channel.Id}> on the **{Context.Guild.Name}** server to join!")
                        .AppendLine("");

                    if (!queue.Games.IsNullOrEmpty())
                        sb.AppendLine($"**Game(s): ** _{string.Join(", ", queue.Games)}_");

                    if (!string.IsNullOrWhiteSpace(queue.Host))
                        sb.AppendLine($"**Server**: _{queue.Host ?? "ra3.se"}:{(queue.Port > 0 ? queue.Port : 27960)}_");

                    var embed = new EmbedBuilder
                    {
                        Title = $"Pickup queue {queue.Name} needs more players",
                        Description = sb.ToString(),
                        Author = new EmbedAuthorBuilder { Name = "pickup-bot" },
                        Color = Color.Orange
                    }.Build();

                    var tasks = users.Select(user => user.SendMessageAsync(embed: embed));

                    await Task.WhenAll(tasks);
                }
            }
        }

        [Command("start")]
        [Summary("Triggers the start of the game by splitting teams and setting up voice channels")]
        public async Task Start([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await VerifyQueueByName(queueName);
            if (queue == null) return;

            var pickupCategory = (ICategoryChannel)Context.Guild.CategoryChannels.FirstOrDefault(c =>
                              c.Name.Equals("Pickup voice channels", StringComparison.OrdinalIgnoreCase))
                           ?? await Context.Guild.CreateCategoryChannelAsync("Pickup voice channels");

            var vcRedTeamName = $"{queue.Name} \uD83D\uDD34";
            var vcBlueTeamName = $"{queue.Name} \uD83D\uDD35";

            var vcRed = await GetOrCreateVoiceChannel(vcRedTeamName, pickupCategory.Id);

            var vcBlue = queue.IsCoop ? null : await GetOrCreateVoiceChannel(vcBlueTeamName, pickupCategory.Id);

            var halfPoint = (int)Math.Ceiling(queue.Subscribers.Count / (double)2);

            var rnd = new Random();
            var users = queue.Subscribers.OrderBy(s => rnd.Next()).Select(u => Context.Guild.GetUser(Convert.ToUInt64(u.Id))).ToList();

            var redTeam = queue.IsCoop ? users : users.Take(halfPoint).ToList();
            var blueTeam = queue.IsCoop ? Enumerable.Empty<SocketGuildUser>() : users.Skip(halfPoint).ToList();

            var redTeamName = $"{(queue.IsCoop ? "Coop" : "Red")} Team \uD83D\uDD34";

            queue.Teams.Add(new Team
            {
                Name = redTeamName,
                Subscribers = redTeam.Select(w => new Subscriber { Id = w.Id, Name = GetNickname(w) }).ToList(),
                VoiceChannel = new KeyValuePair<string, ulong?>(vcRedTeamName, vcRed.Id)
            });

            if (!queue.IsCoop)
            {
                const string blueTeamName = "Blue Team \uD83D\uDD35";
                queue.Teams.Add(new Team
                {
                    Name = blueTeamName,
                    Subscribers = blueTeam.Select(w => new Subscriber { Id = w.Id, Name = GetNickname(w) }).ToList(),
                    VoiceChannel = new KeyValuePair<string, ulong?>(vcBlueTeamName, vcBlue?.Id)

                });
            }

            queue.Started = true;
            await _queueRepository.UpdateQueue(queue);
            await PrintTeams(queue);

            TriggerDelayedRconNotification(queue);
        }

        [Command("teams"), Alias("team")]
        [Summary("Lists the teams of a started pickup queue")]
        public async Task Teams([Name("Queue name"), Remainder] string queueName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await VerifyQueueByName(queueName);
            if (queue == null) return;

            await PrintTeams(queue);

            await TriggerRconNotification(queue);
        }

        [Command("stop")]
        [Summary("Triggers the end of the game by removing voice channels and removing the queue")]
        public async Task Stop([Name("Queue name"), Summary("Queue name")]string queueName)
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var queue = await VerifyQueueByName(queueName);
            if (queue == null) return;

            var voiceIds = queue.Teams.Select(w => w.VoiceChannel.Value).ToList();
            if (voiceIds.Any())
            {
                foreach (var voiceId in voiceIds)
                {
                    if (!voiceId.HasValue) continue;

                    var vc = (IVoiceChannel)Context.Guild.GetVoiceChannel(voiceId.Value);
                    if (vc == null) continue;
                    await vc.DeleteAsync().ConfigureAwait(false);
                }
            }

            await Delete(queueName);
        }

        private async Task<IVoiceChannel> GetOrCreateVoiceChannel(string name, ulong categoryId)
        {
            return (IVoiceChannel)Context.Guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? await Context.Guild.CreateVoiceChannelAsync(name, properties => properties.CategoryId = categoryId);
        }

        private async Task NotifyUsers(PickupQueue queue, string serverName, params SocketGuildUser[] users)
        {
            var usersList = string.Join(Environment.NewLine, queue.Subscribers.Where(u => u.Id != Context.User.Id).Select(u => $@"  - {u.Name}"));
            var header = $"**Contact your teammates on the \"{serverName}\" server and glhf!**";
            var remember = "**Remember**" +
                           $"{Environment.NewLine}" +
                           $"Remember to do `!leave {queue.Name}` if/when you leave the game to make room for those in the waiting list!";

            var embed = new EmbedBuilder
            {
                Title = $"Queue {queue.Name} is ready to go!",
                Description = $@"{header}{Environment.NewLine}{usersList}{Environment.NewLine}{remember}",
                Footer = new EmbedFooterBuilder { Text = $"Provided by pickup-bot - {serverName}" },
                Color = Color.Orange
            }.Build();

            foreach (var user in users)
            {
                await user.SendMessageAsync(embed: embed);
            }
        }

        private static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => w.Name).ToList();
            if ((queue.MaxInQueue) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.MaxInQueue) - queue.Subscribers.Count));

            //if queue found and user is in queue
            return string.Join(", ", subscribers);
        }

        private async Task<PickupQueue> VerifyQueueByName(string queueName)
        {
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());

            if (queue != null) return queue;

            await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
            return null;
        }

        private async Task<bool> VerifyUserFlaggedStatus()
        {
            var flagged = await _flagRepository.IsFlagged((IGuildUser)Context.User);
            if (flagged == null) return true;

            var sb = new StringBuilder()
                .AppendLine("You have been flagged which means that you can't join or create queues.")
                .AppendLine("**Reason**")
                .AppendLine($"_{flagged.Reason}_");

            var embed = new EmbedBuilder
            {
                Title = "You are flagged",
                Description = sb.ToString(),
                Color = Color.Orange
            }.Build();
            await ReplyAsync(embed: embed).AutoRemoveMessage(10);

            return false;
        }

        private async Task PrintTeams(PickupQueue queue)
        {
            if (!queue.Started || queue.Teams.IsNullOrEmpty()) return;

            foreach (var team in queue.Teams)
            {
                var sb = new StringBuilder()
                    .AppendLine("**Teammates:**")
                    .AppendLine($"{string.Join(Environment.NewLine, team.Subscribers.Select(w => w.Name))}")
                    .AppendLine("")
                    .AppendLine("Your designated voice channel:")
                    .AppendLine($"[<#{team.VoiceChannel.Value}>](https://discordapp.com/channels/{Context.Guild.Id}/{team.VoiceChannel.Value})");

                await ReplyAsync(embed: new EmbedBuilder
                {
                    Title = team.Name,
                    Description = sb.ToString(),
                    Color = Color.Red
                }.Build()).AutoRemoveMessage(120);
            }
        }

        private void TriggerDelayedRconNotification(PickupQueue queue)
        {
            // 2 minute delay message
            AsyncUtilities.DelayAction(TimeSpan.FromMinutes(2), async t => { await TriggerRconNotification(queue); });

            // 4 minute delay message
            AsyncUtilities.DelayAction(TimeSpan.FromMinutes(4), async t => { await TriggerRconNotification(queue); });
        }

        private async Task TriggerRconNotification(PickupQueue queue)
        {
            if (!queue.Rcon) return;
            if (!string.IsNullOrWhiteSpace(queue.Host) &&
                !queue.Host.Equals(_rconHost, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                var redTeam = queue.Teams.FirstOrDefault();
                var blueTeam = queue.Teams.LastOrDefault();
                if (string.IsNullOrWhiteSpace(_rconPassword) || string.IsNullOrWhiteSpace(_rconHost) || _rconPort == 0) return;

                var command = $"say \"^2Pickup '^3{queue.Name}^2' has started! " +
                              $"^1RED TEAM: ^5{string.Join(", ", redTeam.Subscribers.Select(w => w.Name))} ^7- " +
                              $"^4BLUE TEAM: ^5{string.Join(", ", blueTeam.Subscribers.Select(w => w.Name))}\"";

                await RCON.UDPSendCommand(command, _rconHost, _rconPassword, _rconPort, true);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static string GetNickname(IUser user) =>
            user switch
            {
                IGuildUser guildUser => guildUser.Nickname ?? guildUser.Username,
                IGroupUser groupUser => groupUser.Username,
                ISelfUser selfUser => selfUser.Username,
                _ => user.Username
            };

        private static string GetMention(IMentionable user) =>
            user switch
            {
                IGuildUser guildUser => guildUser.Mention,
                IGroupUser groupUser => groupUser.Mention,
                ISelfUser selfUser => selfUser.Mention,
                _ => user.Mention
            };

        private static bool IsInPickupChannel(IChannel channel) =>
            channel.Name.StartsWith("pickup", StringComparison.OrdinalIgnoreCase);

    }
}
