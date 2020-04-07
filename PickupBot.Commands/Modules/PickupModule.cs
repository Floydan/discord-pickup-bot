﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Commands.Models;
using PickupBot.Commands.Repositories;

namespace PickupBot.Commands.Modules
{
    [Group("pickup")]
    public class PickupModule : ModuleBase<SocketCommandContext>
    {
        private readonly IQueueRepository _queueRepository;

        public PickupModule(IQueueRepository queueRepository)
        {
            _queueRepository = queueRepository;
        }

        [Command("create")]
        public async Task Create(string queueName, int teamSize = 1)
        {
            if (teamSize == 1 && Regex.IsMatch(queueName, @"^\d+", RegexOptions.Singleline))
            {
                var match = Regex.Match(queueName, @"^(?<number>(\d+))");
                if (match.Success)
                    int.TryParse(match.Value, out teamSize);
            }

            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id);

            if (queue != null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' already exists!`");
                return;
            }

            await _queueRepository.AddQueue(new PickupQueue
            {
                Name = queueName,
                GuildId = Context.Guild.Id,
                OwnerName = Context.User.Username,
                OwnerId = Context.User.Id,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                TeamSize = teamSize,
                Subscribers = new List<Subscriber> { new Subscriber { Id = Context.User.Id, Name = Context.User.Username } }
            });

            await Context.Channel.SendMessageAsync($"`Queue '{queueName}' was added by {Context.User.Username}`");
        }

        [Command("add")]
        public async Task Add(string queueName)
        {
            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id);
            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            if (queue.Subscribers.Count == queue.TeamSize * 2 && queue.WaitingList.All(w => w.Id != Context.User.Id))
            {
                queue.Updated = DateTime.UtcNow;
                queue.WaitingList.Add(new Subscriber { Id = Context.User.Id, Name = Context.User.Username });

                await _queueRepository.UpdateQueue(queue);
            }

            if (queue.Subscribers.All(w => w.Id != Context.User.Id))
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Add(new Subscriber { Id = Context.User.Id, Name = Context.User.Username });

                //if queue found
                await _queueRepository.UpdateQueue(queue);
            }

            await Context.Channel.SendMessageAsync($"`{queueName} - {ParseSubscribers(queue)}`");
        }

        [Command("leave")]
        [Alias("quit")]
        public async Task Leave(string queueName)
        {
            //find queue with name {queueName}
            var queue = await _queueRepository.FindQueue(queueName, Context.Guild.Id);
            if (queue == null)
            {
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`");
                return;
            }

            await LeaveInternal(queue);
        }

        [Command("remove")]
        [Alias("del", "cancel")]
        public async Task Remove(string queueName)
        {
            var result = await _queueRepository.RemoveQueue(Context.User, queueName, Context.Guild.Id);
            var message = result ? $"`Queue '{queueName}' has been canceled`" : $"`Queue with the name '{queueName}' doesn't exists!`";
            await Context.Channel.SendMessageAsync(message);
        }

        [Command("clear")]
        public async Task Clear()
        {
            //find queues with user in it
            var allQueues = await _queueRepository.AllQueues(Context.Guild.Id);

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
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} - You have been removed from all queues");
                await List();
            }
        }

        [Command("list")]
        public async Task List()
        {
            //find all active queues
            var queues = await _queueRepository.AllQueues(Context.Guild.Id);
            //if queues found
            var pickupQueues = queues as PickupQueue[] ?? queues.ToArray();
            if (!pickupQueues.Any())
                return;

            var ordered = pickupQueues.OrderByDescending(w => w.Readiness);

            var description = string.Join(
                Environment.NewLine,
                ordered.Select((q, i) =>
                    $"{i + 1}. **{q.Name}** by _{q.OwnerName}_ [{q.Subscribers.Count}/{q.TeamSize * 2}] - {ParseSubscribers(q)} {(q.WaitingList.Any() ? $"- waiting: **{q.WaitingList.Count}**" : "")}"
                ));

            var embed = new EmbedBuilder()
            {
                Title = "Active queues",
                Description = description
            };
            await Context.Channel.SendMessageAsync("", embed: embed.Build());

            //if no queue found or user is not in queue, inform the user
        }

        private static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => $"[{w.Name}]").ToList();
            if ((queue.TeamSize * 2) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.TeamSize * 2) - queue.Subscribers.Count));

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

                    var user = await Context.Channel.GetUserAsync(next.Id);
                    if (user != null)
                    {
                        await ReplyAsync(
                            $"`{user.Mention} you have been added to '{queue.Name}' since {subscriber.Name} left the queue.`");
                    }
                }
                if (!queue.Subscribers.Any() && !queue.WaitingList.Any())
                    await _queueRepository.RemoveQueue(Context.User, queue.Name, queue.GuildId); //Try to remove queue if its empty and its the owner leaving.
                else
                {
                    await _queueRepository.UpdateQueue(queue);

                    if (notify)
                        await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`");

                    return queue;
                }
            }

            return null;
        }
    }
}
