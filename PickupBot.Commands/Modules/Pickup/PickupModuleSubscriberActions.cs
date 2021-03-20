using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using PickupBot.Commands.Constants;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Commands.Infrastructure.Services;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;

// ReSharper disable MemberCanBePrivate.Global

namespace PickupBot.Commands.Modules.Pickup
{
    [Name("Pickup subscriber actions")]
    [Summary("Commands for handling pickup subscriber actions")]
    public class PickupSubscriberModule : ModuleBase<SocketCommandContext>
    {
        private readonly IQueueRepository _queueRepository;
        private readonly ISubscriberCommandService _subscriberCommandService;
        private readonly IMiscCommandService _miscCommandService;

        public PickupSubscriberModule(
            IQueueRepository queueRepository, 
            ISubscriberCommandService subscriberCommandService, 
            IMiscCommandService miscCommandService
        )
        {
            _queueRepository = queueRepository;
            _subscriberCommandService = subscriberCommandService;
            _miscCommandService = miscCommandService;
        }

        [Command("add")]
        [Summary("Take a spot in a pickup queue, if the queue is full you are placed on the waiting list.")]
        public async Task Add([Name("Queue name"), Summary("Queue name"), Remainder]string queueName = "")
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;
            
            queueName = queueName.Trim(' ', '"').Trim();

            //find queue with name {queueName}
            await _subscriberCommandService.Add(queueName, Context.Channel, (IGuildUser)Context.User, Context.Message.Reference);
        }

        [Command("remove")]
        [Alias("quit", "ragequit")]
        [Summary("Leave a queue, freeing up a spot.")]
        public async Task Remove([Name("Queue name"), Summary("Optional, if empty the !clear command will be used."), Remainder] string queueName = "")
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;
            
            queueName = queueName.Trim(' ', '"').Trim();

            if (string.IsNullOrWhiteSpace(queueName))
            {
                await Clear();
                return;
            }

            //find queue with name {queueName}
            var queue = await _miscCommandService.VerifyQueueByName(queueName, (IGuildChannel)Context.Channel);
            if (queue == null)
            {
                return;
            }

            await _subscriberCommandService.Leave(queue, Context.Channel, (IGuildUser)Context.User, messageReference: Context.Message.Reference);
        }

        [Command("clear")]
        [Alias("clean")]
        [Summary("Leave all queues you have subscribed to, including waiting lists")]
        public async Task Clear()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel)) return;

            //find queues with user in it
            var allQueues = await _queueRepository.AllQueues(Context.Guild.Id.ToString());

            var matchingQueues = allQueues.Where(q => q.Subscribers.Any(s => s.Id == Context.User.Id) || q.WaitingList.Any(w => w.Id == Context.User.Id));

            var pickupQueues = matchingQueues as PickupQueue[] ?? matchingQueues.ToArray();
            if (pickupQueues.Any())
            {
                foreach (var queue in pickupQueues)
                {
                    queue.WaitingList.RemoveAll(w => w.Id == Context.User.Id);
                    queue.Updated = DateTime.UtcNow;

                    var updatedQueue = await _subscriberCommandService.Leave(queue, Context.Channel, (IGuildUser)Context.User, false);

                    updatedQueue ??= queue;

                    if (!updatedQueue.Subscribers.Any() && !updatedQueue.WaitingList.Any())
                    {
                        await _queueRepository.RemoveQueue(updatedQueue.Name, updatedQueue.GuildId); //Try to remove queue if its empty.

                        if (string.IsNullOrEmpty(queue.StaticMessageId)) continue;
                        var queuesChannel = await PickupHelpers.GetPickupQueuesChannel(Context.Guild);
                        await queuesChannel.DeleteMessageAsync(Convert.ToUInt64(queue.StaticMessageId));
                    }
                    else
                        await _queueRepository.UpdateQueue(updatedQueue);
                }

                //if queues found and user is in queue
                await Context.Channel.SendMessageAsync(
                        $"{PickupHelpers.GetMention(Context.User)} - You have been removed from all queues",
                        messageReference: new MessageReference(Context.Message.Id))
                    .AutoRemoveMessage(10);
            }
        }

        [Command("subscribe")]
        [Summary("Subscribes or unsubscribes the user to the promote role to get notifications when queues are created of when the !promote command is used")]
        public async Task Subscribe()
        {
            if (!PickupHelpers.IsInPickupChannel((IGuildChannel)Context.Channel))
                return;

            var role = Context.Guild.Roles.FirstOrDefault(w => w.Name == RoleNames.PickupPromote);
            if (role == null)
                return; //Failed to get or create role;

            var user = (IGuildUser)Context.User;

            if (user.RoleIds.Any(w => w == role.Id))
            {
                await user.RemoveRoleAsync(role);
                await Context.Message.ReplyAsync($"{PickupHelpers.GetMention(user)} - you are no longer subscribed to get notifications on `!promote`")
                    .AutoRemoveMessage(10);
            }
            else
            {
                await user.AddRoleAsync(role);
                await Context.Message.ReplyAsync(
                        $"{PickupHelpers.GetMention(user)} - you are now subscribed to get notifications on `!promote`")
                    .AutoRemoveMessage(10);
            }
        }
    }
}
