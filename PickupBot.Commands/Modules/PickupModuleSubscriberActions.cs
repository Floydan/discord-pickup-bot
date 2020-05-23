using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
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
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            //find queue with name {queueName}
            await AddInternal(queueName, Context.Guild, Context.Channel, (IGuildUser)Context.User);
        }

        [Command("remove")]
        [Alias("quit", "ragequit")]
        [Summary("Leave a queue, freeing up a spot.")]
        public async Task Remove([Name("Queue name"), Summary("Optional, if empty the !clear command will be used."), Remainder] string queueName = "")
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
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

            await LeaveInternal(queue, Context.Channel, (IGuildUser)Context.User);
        }

        [Command("clear")]
        [Alias("clean")]
        [Summary("Leave all queues you have subscribed to, including waiting lists")]
        public async Task Clear()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            //find queues with user in it
            var allQueues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());

            var matchingQueues = allQueues.Where(q => q.Subscribers.Any(s => s.Id == Context.User.Id) || q.WaitingList.Any(w => w.Id == Context.User.Id));

            var pickupQueues = matchingQueues as PickupQueue[] ?? matchingQueues.ToArray();
            if (pickupQueues.Any())
            {
                foreach (var queue in pickupQueues)
                {
                    var updatedQueue = await LeaveInternal(queue, Context.Channel, (IGuildUser)Context.User, false);

                    updatedQueue ??= queue;

                    updatedQueue.WaitingList.RemoveAll(w => w.Id == Context.User.Id);
                    updatedQueue.Updated = DateTime.UtcNow;

                    if (!updatedQueue.Subscribers.Any() && !updatedQueue.WaitingList.Any())
                        await _queueRepository.RemoveQueue(updatedQueue.Name, updatedQueue.GuildId); //Try to remove queue if its empty.
                    else
                        await _queueRepository.UpdateQueue(updatedQueue);
                }

                //if queues found and user is in queue
                await Context.Channel.SendMessageAsync($"{PickupHelpers.GetMention(Context.User)} - You have been removed from all queues").AutoRemoveMessage(10);
            }
        }

        [Command("subscribe")]
        [Summary("Subscribes or unsubscribes the user to the promote role to get notifications when queues are created of when the !promote command is used")]
        public async Task Subscribe()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote");
            if (role == null)
                return; //Failed to get or create role;

            var user = (IGuildUser)Context.User;

            if (user.RoleIds.Any(w => w == role.Id))
            {
                await user.RemoveRoleAsync(role);
                await ReplyAsync($"{PickupHelpers.GetMention(user)} - you are no longer subscribed to get notifications on `!promote`").AutoRemoveMessage(10);
            }
            else
            {
                await user.AddRoleAsync(role);
                await ReplyAsync($"{PickupHelpers.GetMention(user)} - you are now subscribed to get notifications on `!promote`").AutoRemoveMessage(10);
            }
        }
    }
}
