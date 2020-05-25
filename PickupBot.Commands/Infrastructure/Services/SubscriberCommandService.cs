using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot.Commands.Infrastructure.Services
{
    public class SubscriberCommandService : ISubscriberCommandService
    {
        private readonly IQueueRepository _queueRepository;
        private readonly ISubscriberActivitiesRepository _activitiesRepository;
        private readonly IListCommandService _listCommandService;
        private readonly IMiscCommandService _miscCommandService;

        public SubscriberCommandService(
            IQueueRepository queueRepository,
            ISubscriberActivitiesRepository activitiesRepository,
            IListCommandService listCommandService,
            IMiscCommandService miscCommandService
        )
        {
            _queueRepository = queueRepository;
            _activitiesRepository = activitiesRepository;
            _listCommandService = listCommandService;
            _miscCommandService = miscCommandService;
        }
        

        public async Task Add(string queueName, ISocketMessageChannel channel, IGuildUser user)
        {
            var guild = (SocketGuild)user.Guild;
            PickupQueue queue;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                queue = await _queueRepository.FindQueue(queueName, guild.Id.ToString()).ConfigureAwait(false);
            }
            else
            {
                var queues = (await _queueRepository.AllQueues(guild.Id.ToString()).ConfigureAwait(false)).OrderByDescending(w => w.Readiness).ToList();
                queue = queues.Any(q => q.Readiness < 100) ? queues.FirstOrDefault(q => q.Readiness < 100) : queues.FirstOrDefault();
            }

            if (queue == null)
            {
                await channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10).ConfigureAwait(false);
                return;
            }

            if (!await _miscCommandService.VerifyUserFlaggedStatus(user, channel).ConfigureAwait(false))
                return;

            if (queue.Subscribers.Any(w => w.Id == user.Id))
            {
                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`").ConfigureAwait(false);
                return;
            }

            var activity = await _activitiesRepository.Find(user).ConfigureAwait(false);
            activity.PickupAdd += 1;
            
            queue.Updated = DateTime.UtcNow;
            queue.Subscribers.Add(new Subscriber { Id = user.Id, Name = PickupHelpers.GetNickname(user) });

            if (queue.Subscribers.Count >= queue.MaxInQueue)
            {
                if (queue.WaitingList.All(w => w.Id != user.Id))
                {
                    queue = await _listCommandService.SaveStaticQueueMessage(queue, guild).ConfigureAwait(false);

                    await channel.SendMessageAsync($"`You have been added to the '{queue.Name}' waiting list`").AutoRemoveMessage(10).ConfigureAwait(false);
                }
                else
                {
                    await channel.SendMessageAsync($"`You are already on the '{queue.Name}' waiting list`").AutoRemoveMessage(10).ConfigureAwait(false);
                }
            }
            else
            {
                queue = await _listCommandService.SaveStaticQueueMessage(queue, guild).ConfigureAwait(false);

                if (queue.Subscribers.Count == queue.MaxInQueue)
                {
                    await PickupHelpers.NotifyUsers(
                        queue, 
                        guild.Name, 
                        user, 
                        queue.Subscribers.Select(s => guild.GetUser(s.Id)).Where(u => u != null).ToArray()).ConfigureAwait(false);
                }

                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`").ConfigureAwait(false);
            }
            
            await _queueRepository.UpdateQueue(queue).ConfigureAwait(false);
        }

        public async Task<PickupQueue> Leave(PickupQueue queue, ISocketMessageChannel channel, IGuildUser user, bool notify = true)
        {
            var guild = (SocketGuild)user.Guild;
            var subscriber = queue.Subscribers.FirstOrDefault(s => s.Id == user.Id);
            if (subscriber != null)
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Remove(subscriber);

                await MoveUserFromWaitingListToSubscribers(queue, subscriber, channel, guild).ConfigureAwait(false);

                if (!queue.Subscribers.Any() && !queue.WaitingList.Any())
                {
                    notify = await _listCommandService.DeleteEmptyQueue(queue, guild, channel, notify).ConfigureAwait(false);
                }
                else
                {
                    queue = await _listCommandService.SaveStaticQueueMessage(queue, guild).ConfigureAwait(false);
                    await _queueRepository.UpdateQueue(queue).ConfigureAwait(false);
                }
            }

            if (notify)
                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`").AutoRemoveMessage(10).ConfigureAwait(false);

            return queue;
        }
        
        private static async Task MoveUserFromWaitingListToSubscribers(PickupQueue queue, Subscriber subscriber, ISocketMessageChannel channel, SocketGuild guild)
        {
            if (queue.WaitingList.Any())
            {
                var next = queue.WaitingList.First();
                queue.WaitingList.RemoveAt(0);

                queue.Subscribers.Add(next);

                var nextUser = guild.GetUser(next.Id);
                if (nextUser != null)
                {
                    await channel.SendMessageAsync($"{PickupHelpers.GetMention(nextUser)} - you have been added to '{queue.Name}' since {subscriber.Name} has left.").AutoRemoveMessage().ConfigureAwait(false);
                    if (queue.Started)
                    {
                        var team = queue.Teams.FirstOrDefault(w => w.Subscribers.Exists(s => s.Id == subscriber.Id));
                        if (team != null)
                        {
                            team.Subscribers.Remove(team.Subscribers.Find(s => s.Id == subscriber.Id));
                            team.Subscribers.Add(new Subscriber { Name = PickupHelpers.GetNickname(nextUser), Id = nextUser.Id });
                            await channel.SendMessageAsync($"{PickupHelpers.GetMention(nextUser)} - you are on the {team.Name}")
                                .AutoRemoveMessage()
                                .ConfigureAwait(false);
                        }
                    }

                    await PickupHelpers.NotifyUsers(queue, guild.Name, nextUser).ConfigureAwait(false);
                }
            }
        }
    }

    public interface ISubscriberCommandService
    {
        Task Add(string queueName, ISocketMessageChannel channel, IGuildUser user);
        Task<PickupQueue> Leave(PickupQueue queue, ISocketMessageChannel channel, IGuildUser user, bool notify = true);
    }
}
