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
            [Summary("Queue name")] string queueName,
            [Summary("Optional team size (how many are in each team **NOT** total number of players), use if your queue name doesn't start with a number e.g. 2v2")]
            int teamSize = 1)
        {
            if (teamSize == 1 && Regex.IsMatch(queueName, @"^\d+", RegexOptions.Singleline))
            {
                var match = Regex.Match(queueName, @"^(?<number>(\d+))");
                if (match.Success)
                    int.TryParse(match.Value, out teamSize);
            }

            if (teamSize > 16)
                teamSize = 16;

            var flagged = await _flagRepository.IsFlagged((IGuildUser)Context.User);
            if (flagged != null)
            {
                var embed = new EmbedBuilder
                {
                    Title = "You are flagged",
                    Description = $"You have been flagged which means that you can't join or create queues.{Environment.NewLine}**Reason**{Environment.NewLine}_{flagged.Reason}_",
                    Color = Color.Orange
                }.Build();
                await ReplyAsync(embed: embed);
                return;
            }

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
        public async Task Add([Summary("Queue name"), Remainder]string queueName)
        {
            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id.ToString());
            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            var flagged = await _flagRepository.IsFlagged((IGuildUser)Context.User);
            if (flagged != null)
            {
                var embed = new EmbedBuilder
                {
                    Title = "You are flagged",
                    Description = $"You have been flagged which means that you can't join or create queues.{Environment.NewLine}**Reason**{Environment.NewLine}_{flagged.Reason}_",
                    Color = Color.Orange
                }.Build();
                await ReplyAsync(embed: embed);
                return;
            }

            if (queue.Subscribers.Count >= queue.MaxInQueue)
            {
                if (queue.WaitingList.All(w => w.Id != Context.User.Id))
                {
                    queue.Updated = DateTime.UtcNow;
                    queue.WaitingList.Add(new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) });

                    await _queueRepository.UpdateQueue(queue);

                    await ReplyAsync($"`You have been added to the waiting list for '{queue.Name}'`");
                }
                else
                {
                    await ReplyAsync($"`You are already on the waiting list for queue '{queue.Name}'`");
                }
            }
            else if (queue.Subscribers.All(w => w.Id != Context.User.Id))
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Add(new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) });

                //if queue found
                await _queueRepository.UpdateQueue(queue);

                if (queue.Subscribers.Count == queue.MaxInQueue)
                {
                    await NotifyUsers(queue, Context.Guild.Name, queue.Subscribers.Select(subscriber => Context.Guild.GetUser(subscriber.Id)).ToArray());
                }
            }

            await Context.Channel.SendMessageAsync($"`{queueName} - {ParseSubscribers(queue)}`");
        }

        private async Task NotifyUsers(PickupQueue queue, string serverName, params SocketGuildUser[] users)
        {
            var usersList = string.Join(Environment.NewLine, queue.Subscribers.Where(u => u.Id != Context.User.Id).Select(u => $@"  - {u.Name}"));
            var header = $"**Contact your teammates on the \"{serverName}\" server and glhf!**";
            var remember =
                $"**Remember** {Environment.NewLine}Remember to do `!leave {queue.Name}` if/when you leave the game to make room for those in the waiting list!";

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

        [Command("leave")]
        [Alias("quit")]
        [Summary("Leave a queue, freeing up a spot.")]
        public async Task Leave([Summary("Queue name"), Remainder] string queueName)
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
        public async Task Remove([Summary("Queue name"), Remainder] string queueName)
        {
            var result = await _queueRepository.RemoveQueue(Context.User, queueName, Context.Guild.Id.ToString());
            var message = result ? $"`Queue '{queueName}' has been canceled`" : $"`Queue with the name '{queueName}' doesn't exists or you are not the owner of the queue!`";
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
        public async Task WaitList([Summary("Queue name"), Remainder] string queueName)
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

        private static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => w.Name).ToList();
            if ((queue.MaxInQueue) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.MaxInQueue) - queue.Subscribers.Count));

            //if queue found and user is in queue
            return string.Join(", ", subscribers);
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
                        await ReplyAsync(
                            $"`{GetMention(user)} you have been added to '{queue.Name}' since {subscriber.Name} has left.`");
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

        private static string GetNickname(IUser user) =>
            user switch
            {
                IGuildUser guildUser => guildUser.Nickname,
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
