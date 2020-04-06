using System;
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

            await _queueRepository.AddQueue(new PickupQueue()
            {
                Name = queueName,
                GuildId = Context.Guild.Id,
                OwnerName = Context.User.Username,
                OwnerId = Context.User.Id,
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                TeamSize = teamSize,
                Subscribers = new List<string> { Context.User.Username }
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

            if (!queue.Subscribers.Any(w => w.Equals(Context.User.Username, StringComparison.OrdinalIgnoreCase)))
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Add(Context.User.Username);

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

            queue.Updated = DateTime.UtcNow;
            queue.Subscribers.Remove(Context.User.Username);

            //if queue found and user is in queue
            await Context.Channel.SendMessageAsync($"`{queueName} - {ParseSubscribers(queue)}`");

            //if no queue found or user is not in queue, inform the user
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

            //if queues found and user is in queue
            await Context.Channel.SendMessageAsync($"{Context.User.Mention} - You have been removed from all queues");
            await Context.Channel.SendMessageAsync($"List all new queue statuses");

            //if no queue found or user is not in queue, inform the user
        }

        [Command("list")]
        public async Task List()
        {
            //find all active queues
            var queues = await _queueRepository.AllQueues();
            //if queues found
            if (queues == null || !queues.Any())
                return;

            var ordered = queues.OrderByDescending(w => w.Readiness);

            var description = string.Join(
                Environment.NewLine,
                ordered.Select((q, i) => $"{i + 1}. **{q.Name}** by _{q.OwnerName}_ [{q.Subscribers.Count}/{q.TeamSize * 2}] - {ParseSubscribers(q)}"));

            var embed = new EmbedBuilder()
            {
                Title = "Active queues",
                Description = description
            };
            await Context.Channel.SendMessageAsync($@"", embed: embed.Build());

            //if no queue found or user is not in queue, inform the user
        }

        private static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => $"[{w}]").ToList();
            if ((queue.TeamSize * 2) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.TeamSize * 2) - queue.Subscribers.Count));

            //if queue found and user is in queue
            return string.Join(", ", subscribers);
        }
    }
}
