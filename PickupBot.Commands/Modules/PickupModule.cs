using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;
// ReSharper disable MemberCanBePrivate.Global

namespace PickupBot.Commands.Modules
{
    [Name("Pickup")]
    [Summary("Commands for handling pickup queues")]
    public class PickupModule : ModuleBase<SocketCommandContext>
    {
        private readonly IQueueRepository _queueRepository;
        private readonly IFlaggedSubscribersRepository _flagRepository;

        public PickupModule(IQueueRepository queueRepository, IFlaggedSubscribersRepository flagRepository)
        {
            _queueRepository = queueRepository;
            _flagRepository = flagRepository;
        }

        [Command("create")]
        [Summary("Creates a pickup queue")]
        public async Task Create(
            [Name("Queue name"), Summary("Queue name")] string queueName,
            [Name("Team size"), Summary("Team size (how many are in each team **NOT** total number of players)")]
            int teamSize)
        {
            if (teamSize > 16)
                teamSize = 16;

            if (!await VerifyUserFlaggedStatus())
                return;

            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());

            if (queue != null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' already exists!`");
                return;
            }

            await _queueRepository.AddQueue(new PickupQueue(Context.Guild.Id.ToString(), queueName)
            {
                Name = queueName,
                GuildId = Context.Guild.Id.ToString(),
                OwnerName = GetNickname(Context.User),
                OwnerId = Context.User.Id.ToString(),
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                TeamSize = teamSize,
                Subscribers = new List<Subscriber> { new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) } }
            });

            await Context.Channel.SendMessageAsync($"`Queue '{queueName}' was added by {GetNickname(Context.User)}`");
        }

        [Command("add")]
        [Summary("Take a spot in a pickup queue, if the queue is full you are placed on the waiting list.")]
        public async Task Add([Name("Queue name"), Summary("Queue name"), Remainder]string queueName = "")
        {
            //find queue with name {queueName}
            PickupQueue queue = null;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());
            }
            else
            {
                var queues = (await _queueRepository.AllQueues(Context.Guild.Id.ToString())).OrderByDescending(w => w.Readiness).ToList();
                queue = queues.Any(q => q.Readiness < 100) ? queues.FirstOrDefault(q => q.Readiness < 100) : queues.FirstOrDefault();
            }

            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            if (!await VerifyUserFlaggedStatus())
                return;

            if (queue.Subscribers.Any(w => w.Id == Context.User.Id))
            {
                await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`");
                return;
            }

            if (queue.Subscribers.Count >= queue.MaxInQueue)
            {
                if (queue.WaitingList.All(w => w.Id != Context.User.Id))
                {
                    queue.Updated = DateTime.UtcNow;
                    queue.WaitingList.Add(new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) });

                    await _queueRepository.UpdateQueue(queue);

                    await ReplyAsync($"`You have been added to the '{queue.Name}' waiting list`");
                }
                else
                {
                    await ReplyAsync($"`You are already on the '{queue.Name}' waiting list`");
                }

                return;
            }

            queue.Updated = DateTime.UtcNow;
            queue.Subscribers.Add(new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) });

            //if queue found
            await _queueRepository.UpdateQueue(queue);

            if (queue.Subscribers.Count == queue.MaxInQueue)
            {
                await NotifyUsers(queue, Context.Guild.Name, queue.Subscribers.Select(subscriber => Context.Guild.GetUser(subscriber.Id)).ToArray());
            }

            await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`");
        }

        [Command("leave")]
        [Alias("quit")]
        [Summary("Leave a queue, freeing up a spot.")]
        public async Task Leave([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());
            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            await LeaveInternal(queue);
        }

        [Command("remove")]
        [Alias("del", "cancel")]
        [Summary("If you are the creator of the queue you can use this to delete it")]
        public async Task Remove([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            var result = await _queueRepository.RemoveQueue(Context.User, queueName, Context.Guild.Id.ToString());
            var message = result ?
                $"`Queue '{queueName}' has been canceled`" :
                $"`Queue with the name '{queueName}' doesn't exists or you are not the owner of the queue!`";
            await Context.Channel.SendMessageAsync(message);
        }

        [Command("clear")]
        [Summary("Leave all queues you have subscribed to, including waiting lists")]
        public async Task Clear()
        {
            //find queues with user in it
            var allQueues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());

            var matchingQueues = allQueues.Where(q => q.Subscribers.Any(s => s.Id == Context.User.Id) || q.WaitingList.Any(w => w.Id == Context.User.Id));

            var pickupQueues = matchingQueues as PickupQueue[] ?? matchingQueues.ToArray();
            if (pickupQueues.Any())
            {
                foreach (var queue in pickupQueues)
                {
                    var updatedQueue = await LeaveInternal(queue, false);

                    updatedQueue ??= queue;

                    updatedQueue.WaitingList.RemoveAll(w => w.Id == Context.User.Id);
                    updatedQueue.Updated = DateTime.UtcNow;

                    if (!updatedQueue.Subscribers.Any() && !updatedQueue.WaitingList.Any())
                        await _queueRepository.RemoveQueue(Context.User, updatedQueue.Name, updatedQueue.GuildId); //Try to remove queue if its empty and its the owner leaving.
                    else
                        await _queueRepository.UpdateQueue(updatedQueue);
                }

                //if queues found and user is in queue
                await Context.Channel.SendMessageAsync($"{GetMention(Context.User)} - You have been removed from all queues");
            }
        }

        [Command("list")]
        [Summary("List all active queues")]
        public async Task List()
        {
            //find all active queues
            var queues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());
            Embed embed;
            //if queues found
            var pickupQueues = queues as PickupQueue[] ?? queues.ToArray();
            if (!pickupQueues.Any())
            {
                embed = new EmbedBuilder()
                {
                    Title = "Active queues",
                    Description = "There are no active pickup queues at this time, maybe you should `!create` one \uD83D\uDE09",
                    Color = Color.Orange
                }.Build();

                await Context.Channel.SendMessageAsync(embed: embed);
                return;
            }

            var ordered = pickupQueues.OrderByDescending(w => w.Readiness);

            var description = string.Join(
                Environment.NewLine,
                ordered.Select((q, i) =>
                    $"{i + 1}. **{q.Name}** by _{q.OwnerName}_ [{q.Subscribers.Count}/{q.MaxInQueue}] - {ParseSubscribers(q)} {(q.WaitingList.Any() ? $"- waiting: **{q.WaitingList.Count}**" : "")}"
                ));

            embed = new EmbedBuilder()
            {
                Title = "Active queues",
                Description = description,
                Color = Color.Orange
            }.Build();
            await Context.Channel.SendMessageAsync(embed: embed);
        }

        [Command("waitlist")]
        [Summary("Lists all the players in a given queues wait list")]
        public async Task WaitList([Name("Queue name"), Summary("Queue name"), Remainder] string queueName)
        {
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());

            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            var waitlist = string.Join($"{Environment.NewLine} ", queue.WaitingList.Select((w, i) => $"{i + 1}: {w.Name}"));
            if (string.IsNullOrWhiteSpace(waitlist))
                waitlist = "No players in the waiting list";

            var embed = new EmbedBuilder()
            {
                Title = $"Players in waiting list for queue {queue.Name}",
                Description = waitlist,
                Color = Color.Orange
            }.Build();
            await Context.Channel.SendMessageAsync(embed: embed);
        }

        [Command("subscribe")]
        [Summary("Subscribes or unsubscribes the user to the promote role to get notifications when queues are created of when the !promote command is used")]
        public async Task Subscribe()
        {
            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote") ??
                         (IRole)await Context.Guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange,
                             false);
            if (role == null)
                return; //Failed to get or create role;

            var user = (IGuildUser)Context.User;

            if (user.RoleIds.Any(w => w == role.Id))
            {
                await user.RemoveRoleAsync(role);
                await ReplyAsync($"{GetMention(user)} - you are no longer subscribed to get notifications on `!promote`");
            }
            else
            {
                await user.AddRoleAsync(role);
                await ReplyAsync($"{GetMention(user)} - you are now subscribed to get notifications on `!promote`");
            }
        }

        [Command("promote")]
        [Summary("Promotes one specific or all queues to the 'promote-role' role")]
        public async Task Promote([Name("Queue name"), Summary("Queue name"), Remainder] string queueName = "")
        {
            PickupQueue queue = null;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());
                if (queue == null)
                {
                    await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                    return;
                }

                if (queue.MaxInQueue <= queue.Subscribers.Count)
                {
                    await ReplyAsync("Queue is full, why the spam?");
                    return;
                }
            }

            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote") ??
                       (IRole)await Context.Guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange,
                           false);
            if (role == null)
                return; //Failed to get or create role;

            await Context.Channel.TriggerTypingAsync();

            var users = Context.Guild.Users.Where(w => w.Roles.Any(r => r.Id == role.Id)).ToList();
            if (!users.Any())
            {
                await ReplyAsync("No users have subscribed using the `!subscribe` command.");
                return;
            }

            if (string.IsNullOrWhiteSpace(queueName))
            {
                var queues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());
                var filtered = queues.Where(q => q.MaxInQueue > q.Subscribers.Count).ToArray();
                if (filtered.Any())
                    await ReplyAsync($"There are {filtered.Length} pickup queues with spots left, check out the `!list`! - {role.Mention}");
            }
            else if (queue != null)
            {
                var voiceChannel = Context.Guild.VoiceChannels.OrderBy(c => c.Position).FirstOrDefault();
                var embed = new EmbedBuilder
                {
                    Title = $"Pickup queue {queue.Name} needs more players",
                    Description = "**Current queue**" +
                                  $"{Environment.NewLine} " +
                                  $"{ParseSubscribers(queue)}" +
                                  $"{Environment.NewLine}{Environment.NewLine}" +
                                  $"**Spots left**: {queue.MaxInQueue - queue.Subscribers.Count}" +
                                  $"{Environment.NewLine}" +
                                  $"**Team size**: {queue.TeamSize}" +
                                  $"{Environment.NewLine}{Environment.NewLine}" +
                                  $"Just run `!add {queue.Name}` in channel <#{Context.Channel.Id}> on the **{Context.Guild.Name}** server to join!"
                                  /*$"voice channel [<#{voiceChannel?.Id}>](https://discordapp.com/channels/{Context.Guild.Id}/{voiceChannel?.Id})"*/,
                    Author = new EmbedAuthorBuilder { Name = "pickup-bot" },
                    Color = Color.Orange
                }.Build();

                var tasks = users.Select(user => user.SendMessageAsync(embed: embed));

                await Task.WhenAll(tasks);
            }
        }

        [Command("start")]
        [Summary("Triggers the start of the game by splitting teams and setting up voice channels")]
        public async Task Start([Name("Queue name"), Summary("Queue name")] string queueName)
        {
            var queue = await VerifyQueueByName(queueName);
            if (queue == null) return;

            if (queue.TeamSize < 2)
                return;

            var pickupCategory = (ICategoryChannel)Context.Guild.CategoryChannels.FirstOrDefault(c =>
                              c.Name.Equals("Pickup voice channels", StringComparison.OrdinalIgnoreCase))
                           ?? await Context.Guild.CreateCategoryChannelAsync("Pickup voice channels",
                               properties => properties.Position = int.MaxValue);

            var vcRedTeamName = $"{queue.Name} - \uD83D\uDD34";
            var vcBlueTeamName = $"{queue.Name} - \uD83D\uDD35";

            var vcRed = (IVoiceChannel)Context.Guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals(vcRedTeamName, StringComparison.OrdinalIgnoreCase))
                          ?? await Context.Guild.CreateVoiceChannelAsync(vcRedTeamName, properties => properties.CategoryId = pickupCategory.Id);

            var vcBlue = (IVoiceChannel)Context.Guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals(vcBlueTeamName, StringComparison.OrdinalIgnoreCase))
                          ?? await Context.Guild.CreateVoiceChannelAsync(vcBlueTeamName, properties => properties.CategoryId = pickupCategory.Id);

            var halfPoint = (int)Math.Ceiling(queue.Subscribers.Count / (double)2);

            var rnd = new Random();
            var users = queue.Subscribers.OrderBy(s => rnd.Next()).Select(u => Context.Guild.GetUser(Convert.ToUInt64(u.Id))).ToList();

            var redTeam = users.Take(halfPoint);
            var blueTeam = users.Skip(halfPoint);

            await ReplyAsync(embed: new EmbedBuilder
            {
                Title = "Red Team \uD83D\uDD34",
                Description = "**Teammates:**" +
                              $"{Environment.NewLine}" +
                              $"{string.Join(Environment.NewLine, redTeam.Select(GetMention))}" +
                              $"{Environment.NewLine}{Environment.NewLine}" +
                              $"Your designated voice channel:" +
                              $"{Environment.NewLine}" +
                              $"[<#{vcRed.Id}>](https://discordapp.com/channels/{Context.Guild.Id}/{vcRed.Id})",
                Color = Color.Red
            }.Build());

            await ReplyAsync(embed: new EmbedBuilder
            {
                Title = "Blue Team \uD83D\uDD35",
                Description = "**Teammates:**" +
                              $"{Environment.NewLine}" +
                              $"{string.Join(Environment.NewLine, blueTeam.Select(GetMention))}" +
                              $"{Environment.NewLine}{Environment.NewLine}" +
                              $"Your designated voice channel:" +
                              $"{Environment.NewLine}" +
                              $"[<#{vcBlue.Id}>](https://discordapp.com/channels/{Context.Guild.Id}/{vcBlue.Id})",
                Color = Color.Blue
            }.Build());
        }


        [Command("stop")]
        [Summary("Triggers the end of the game by removing voice channels and removing the queue")]
        public async Task Stop([Name("Queue name"), Summary("Queue name")]string queueName)
        {
            var queue = await VerifyQueueByName(queueName);
            if (queue == null) return;

            if (queue.TeamSize < 2)
                return;

            var vcRedTeamName = $"{queue.Name} \uD83D\uDD34";
            var vcBlueTeamName = $"{queue.Name} \uD83D\uDD35";

            var vcRed = (IVoiceChannel)Context.Guild.VoiceChannels.FirstOrDefault(c =>
               c.Name.Equals(vcRedTeamName, StringComparison.OrdinalIgnoreCase));
            var vcBlue = (IVoiceChannel)Context.Guild.VoiceChannels.FirstOrDefault(c =>
               c.Name.Equals(vcBlueTeamName, StringComparison.OrdinalIgnoreCase));

            if (vcRed != null)
                await vcRed.DeleteAsync();
            if (vcBlue != null)
                await vcBlue.DeleteAsync();

            await Remove(queueName);
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

            await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
            return null;
        }

        private async Task<PickupQueue> LeaveInternal(PickupQueue queue, bool notify = true)
        {
            var subscriber = queue.Subscribers.FirstOrDefault(s => s.Id == Context.User.Id);
            if (subscriber != null)
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Remove(subscriber);

                if (queue.WaitingList.Any())
                {
                    var next = queue.WaitingList.First();
                    queue.WaitingList.RemoveAt(0);

                    queue.Subscribers.Add(next);

                    var user = Context.Guild.GetUser(next.Id);
                    if (user != null)
                    {
                        await ReplyAsync($"{GetMention(user)} - you have been added to '{queue.Name}' since {subscriber.Name} has left.");
                        await NotifyUsers(queue, Context.Guild.Name, user);
                    }
                }
                if (!queue.Subscribers.Any() && !queue.WaitingList.Any())
                    await _queueRepository.RemoveQueue(Context.User, queue.Name, queue.GuildId); //Try to remove queue if its empty and its the owner leaving.
                else
                {
                    await _queueRepository.UpdateQueue(queue);

                    if (notify)
                        await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`");
                }
            }

            return queue;
        }

        private async Task<bool> VerifyUserFlaggedStatus()
        {
            var flagged = await _flagRepository.IsFlagged((IGuildUser)Context.User);
            if (flagged == null) return true;

            var embed = new EmbedBuilder
            {
                Title = "You are flagged",
                Description = $"You have been flagged which means that you can't join or create queues." +
                              $"{Environment.NewLine}" +
                              $"**Reason**" +
                              $"{Environment.NewLine}" +
                              $"_{flagged.Reason}_",
                Color = Color.Orange
            }.Build();
            await ReplyAsync(embed: embed);

            return false;
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

    }
}
