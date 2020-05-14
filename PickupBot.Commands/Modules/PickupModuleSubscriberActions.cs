using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Commands.Extensions;
using PickupBot.Data.Models;

// ReSharper disable MemberCanBePrivate.Global

namespace PickupBot.Commands.Modules
{
    public partial class PickupModule
    {
        [Command("add")]
        [Summary("Take a spot in a pickup queue, if the queue is full you are placed on the waiting list.")]
        public async Task Add([Name("Queue name"), Summary("Queue name"), Remainder]string queueName = "")
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

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
                await Context.Channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
                return;
            }

            if (!await VerifyUserFlaggedStatus())
                return;

            if (queue.Subscribers.Any(w => w.Id == Context.User.Id))
            {
                await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`");
                return;
            }

            var activity = await _activitiesRepository.Find((IGuildUser)Context.User);
            activity.PickupAdd += 1;
            await _activitiesRepository.Update(activity);

            if (queue.Subscribers.Count >= queue.MaxInQueue)
            {
                if (queue.WaitingList.All(w => w.Id != Context.User.Id))
                {
                    queue.Updated = DateTime.UtcNow;
                    queue.WaitingList.Add(new Subscriber { Id = Context.User.Id, Name = GetNickname(Context.User) });

                    await _queueRepository.UpdateQueue(queue);

                    await ReplyAsync($"`You have been added to the '{queue.Name}' waiting list`").AutoRemoveMessage(10);
                }
                else
                {
                    await ReplyAsync($"`You are already on the '{queue.Name}' waiting list`").AutoRemoveMessage(10);
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

        [Command("remove")]
        [Alias("quit", "ragequit")]
        [Summary("Leave a queue, freeing up a spot.")]
        public async Task Remove([Name("Queue name"), Summary("Optional, if empty the !clear command will be used."), Remainder] string queueName = "")
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            if (string.IsNullOrWhiteSpace(queueName))
            {
                await Clear();
                return;
            }

            //find queue with name {queueName}
            var queue = await VerifyQueueByName(queueName);
            if (queue == null)
            {
                return;
            }

            await LeaveInternal(queue);
        }

        [Command("clear")]
        [Alias("clean")]
        [Summary("Leave all queues you have subscribed to, including waiting lists")]
        public async Task Clear()
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

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
                        await _queueRepository.RemoveQueue(updatedQueue.Name, updatedQueue.GuildId); //Try to remove queue if its empty.
                    else
                        await _queueRepository.UpdateQueue(updatedQueue);
                }

                //if queues found and user is in queue
                await Context.Channel.SendMessageAsync($"{GetMention(Context.User)} - You have been removed from all queues").AutoRemoveMessage(10);
            }
        }

        [Command("subscribe")]
        [Summary("Subscribes or unsubscribes the user to the promote role to get notifications when queues are created of when the !promote command is used")]
        public async Task Subscribe()
        {
            if (!IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote") ??
                         (IRole)await Context.Guild.CreateRoleAsync("pickup-promote", GuildPermissions.None, Color.Orange, isHoisted: false, isMentionable: true);
            if (role == null)
                return; //Failed to get or create role;

            var user = (IGuildUser)Context.User;

            if (user.RoleIds.Any(w => w == role.Id))
            {
                await user.RemoveRoleAsync(role);
                await ReplyAsync($"{GetMention(user)} - you are no longer subscribed to get notifications on `!promote`").AutoRemoveMessage(10);
            }
            else
            {
                await user.AddRoleAsync(role);
                await ReplyAsync($"{GetMention(user)} - you are now subscribed to get notifications on `!promote`").AutoRemoveMessage(10);
            }
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
                        await ReplyAsync($"{GetMention(user)} - you have been added to '{queue.Name}' since {subscriber.Name} has left.").AutoRemoveMessage();
                        if (queue.Started)
                        {
                            var team = queue.Teams.FirstOrDefault(w => w.Subscribers.Exists(s => s.Id == subscriber.Id));
                            if (team != null)
                            {
                                team.Subscribers.Remove(team.Subscribers.Find(s => s.Id == subscriber.Id));
                                team.Subscribers.Add(new Subscriber { Name = GetNickname(user), Id = user.Id});
                                await ReplyAsync($"{GetMention(user)} - you are on the {team.Name}").AutoRemoveMessage();
                            }
                        }

                        await NotifyUsers(queue, Context.Guild.Name, user);
                    }
                }

                await _queueRepository.UpdateQueue(queue);

                if (!queue.Subscribers.Any() && !queue.WaitingList.Any())
                {
                    await _queueRepository.RemoveQueue(queue.Name, queue.GuildId); //Try to remove queue if its empty
                    if (notify)
                    {
                        await Context.Channel.SendMessageAsync($"`{queue.Name} has been removed since everyone left.`").AutoRemoveMessage(10);
                        notify = false;
                    }
                }
            }

            if (notify)
                await Context.Channel.SendMessageAsync($"`{queue.Name} - {ParseSubscribers(queue)}`").AutoRemoveMessage(10);

            return queue;
        }
    }
}
