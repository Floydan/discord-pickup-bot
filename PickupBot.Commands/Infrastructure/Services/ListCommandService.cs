using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories;

namespace PickupBot.Commands.Infrastructure.Services
{
    public class ListCommandService : IListCommandService
    {
        private readonly IQueueRepository _queueRepository;
        private readonly ISubscriberActivitiesRepository _activitiesRepository;

        public ListCommandService(IQueueRepository queueRepository, ISubscriberActivitiesRepository activitiesRepository)
        {
            _queueRepository = queueRepository;
            _activitiesRepository = activitiesRepository;
        }

        public async Task<bool> DeleteEmptyQueue(PickupQueue queue, SocketGuild guild, ISocketMessageChannel channel, bool notify)
        {
            var result = await _queueRepository.RemoveQueue(queue.Name, queue.GuildId).ConfigureAwait(false); //Try to remove queue if its empty
            if (result)
            {
                var queuesChannel = await PickupHelpers.GetPickupQueuesChannel(guild).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(queue.StaticMessageId))
                    await queuesChannel.DeleteMessageAsync(Convert.ToUInt64(queue.StaticMessageId)).ConfigureAwait(false);
            }

            if (!notify) return false;

            await channel.SendMessageAsync($"`{queue.Name} has been removed since everyone left.`").AutoRemoveMessage(10).ConfigureAwait(false);

            return false;
        }

        public async Task PrintTeams(PickupQueue queue, ISocketMessageChannel channel, IGuild guild)
        {
            if (!queue.Started || queue.Teams.IsNullOrEmpty()) return;

            foreach (var team in queue.Teams)
            {
                var sb = new StringBuilder()
                    .AppendLine("**Teammates:**")
                    .AppendLine($"{string.Join(Environment.NewLine, team.Subscribers.Select(w => w.Name))}")
                    .AppendLine("")
                    .AppendLine("Your designated voice channel:")
                    .AppendLine($"[<#{team.VoiceChannel.Value}>](https://discordapp.com/channels/{guild.Id}/{team.VoiceChannel.Value})");

                await channel.SendMessageAsync(embed: new EmbedBuilder
                    {
                        Title = team.Name,
                        Description = sb.ToString(),
                        Color = Color.Red
                    }.Build())
                    .AutoRemoveMessage(120)
                    .ConfigureAwait(false);
            }
        }

        public async Task Promote(PickupQueue queue, ITextChannel pickupChannel, IGuildUser user)
        {
            var guild = (SocketGuild)user.Guild;
            var activity = await _activitiesRepository.Find(user).ConfigureAwait(false);
            activity.PickupPromote += 1;
            await _activitiesRepository.Update(activity).ConfigureAwait(false);

            if (queue?.MaxInQueue <= queue?.Subscribers.Count)
            {
                await pickupChannel.SendMessageAsync("Queue is full, why the spam?").AutoRemoveMessage(10).ConfigureAwait(false);
                return;
            }

            var role = guild.Roles.FirstOrDefault(w => w.Name == "pickup-promote");
            if (role == null) return; //Failed to get role;
            
            var users = guild.Users.Where(w => w.Roles.Any(r => r.Id == role.Id)).ToList();
            if (!users.Any())
            {
                await pickupChannel.SendMessageAsync("No users have subscribed using the `!subscribe` command.")
                    .AutoRemoveMessage(10)
                    .ConfigureAwait(false);
                return;
            }

            using (pickupChannel.EnterTypingState())
            {

                if (queue == null)
                {
                    var queues = await _queueRepository.AllQueues(user.GuildId.ToString()).ConfigureAwait(false);
                    var filtered = queues.Where(q => q.MaxInQueue > q.Subscribers.Count).ToArray();
                    if (filtered.Any())
                        await pickupChannel.SendMessageAsync($"There are {filtered.Length} pickup queues with spots left, check out the `!list`! - {role.Mention}")
                            .AutoRemoveMessage()
                            .ConfigureAwait(false);
                }
                else
                {
                    var sb = BuildPromoteMessage(queue, pickupChannel);
                    var embed = new EmbedBuilder
                    {
                        Title = $"Pickup queue {queue.Name} needs more players",
                        Description = sb.ToString(),
                        Author = new EmbedAuthorBuilder { Name = "pickup-bot" },
                        Color = Color.Orange
                    }.Build();

                    foreach (var u in users)
                    {
                        await u.SendMessageAsync(embed: embed).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromMilliseconds(200)).ConfigureAwait(false);
                    }
                }
            }
        }

        private static StringBuilder BuildPromoteMessage(PickupQueue queue, IGuildChannel pickupChannel)
        {
            var sb = new StringBuilder()
                .AppendLine("**Current queue**")
                .AppendLine($"`{PickupHelpers.ParseSubscribers(queue)}`")
                .AppendLine("")
                .AppendLine($"**Spots left**: {queue.MaxInQueue - queue.Subscribers.Count}")
                .AppendLine($"**Team size**: {queue.TeamSize}")
                .AppendLine("")
                .AppendLine($"Just run `!add \"{queue.Name}\"` in channel <#{pickupChannel.Id}> on the **{pickupChannel.Guild.Name}** server to join!")
                .AppendLine("");

            if (!queue.Games.IsNullOrEmpty())
                sb.AppendLine($"**Game(s): ** _{string.Join(", ", queue.Games)}_");

            if (!string.IsNullOrWhiteSpace(queue.Host))
                sb.AppendLine($"**Server**: _{queue.Host ?? "ra3.se"}:{(queue.Port > 0 ? queue.Port : 27960)}_");

            return sb;
        }

        public async Task<PickupQueue> SaveStaticQueueMessage(PickupQueue queue, SocketGuild guild)
        {
            var queuesChannel = await PickupHelpers.GetPickupQueuesChannel(guild).ConfigureAwait(false);

            var user = guild.GetUser(Convert.ToUInt64(queue.OwnerId));

            var embed = CreateStaticQueueMessageEmbed(queue, user);

            AddSubscriberFieldsToStaticQueueMessageFields(queue, embed);
            AddWaitingListFieldsToStaticQueueMessageFields(queue, embed);

            embed.WithFields(
                new EmbedFieldBuilder {Name = "\u200b", Value = "\u200b"},
                new EmbedFieldBuilder
                {
                    Name = "Available actions", 
                    Value = $"\u2705 - Add to pickup / remove from pickup\r\n" +
                            $"\uD83D\uDCE2 - Promote pickup"
                }
            );

            if (string.IsNullOrEmpty(queue.StaticMessageId))
            {
                var message = await queuesChannel.SendMessageAsync(embed: embed.Build());
                await message.AddReactionsAsync(new IEmote[] { new Emoji("\u2705"), new Emoji("\uD83D\uDCE2") }).ConfigureAwait(false); // timer , new Emoji("\u23F2")

                queue.StaticMessageId = message.Id.ToString();
            }
            else
            {
                if (await queuesChannel.GetMessageAsync(Convert.ToUInt64(queue.StaticMessageId)).ConfigureAwait(false) is IUserMessage message)
                    await message.ModifyAsync(m => { m.Embed = embed.Build(); }).ConfigureAwait(false);
            }

            return queue;
        }

        private static EmbedBuilder CreateStaticQueueMessageEmbed(PickupQueue queue, IUser user)
        {
            var embed = new EmbedBuilder
            {
                Title = queue.Name,
                Author = new EmbedAuthorBuilder { Name = PickupHelpers.GetNickname(user), IconUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl() },
                Color = Color.Gold, 
                Fields = new List<EmbedFieldBuilder>
                {
                    new EmbedFieldBuilder { Name = "Created by", Value = PickupHelpers.GetNickname(user), IsInline = true },
                    new EmbedFieldBuilder
                    {
                        Name = "Game(s)",
                        Value = string.Join(", ", queue.Games.IsNullOrEmpty() ? new[] { "No game defined" } : queue.Games),
                        IsInline = true
                    },
                    new EmbedFieldBuilder { Name = "Started", Value = queue.Started ? "Yes" : "No", IsInline = true },
                    new EmbedFieldBuilder { Name = "Host", Value = queue.Host ?? "No host defined", IsInline = true },
                    new EmbedFieldBuilder
                    {
                        Name = "Port",
                        Value = queue.Port == 0 ? "No port defined" : queue.Port.ToString(),
                        IsInline = true
                    },
                    new EmbedFieldBuilder { Name = "Team size", Value = queue.TeamSize, IsInline = true },
                    new EmbedFieldBuilder { Name = "Coop", Value = queue.IsCoop ? "Yes" : "No", IsInline = true },
                    new EmbedFieldBuilder { Name = "Created", Value = queue.Created.ToString("yyyy-MM-dd\r\nHH:mm:ss 'UTC'"), IsInline = true },
                    new EmbedFieldBuilder { Name = "Last updated", Value = queue.Updated.ToString("yyyy-MM-dd\r\nHH:mm:ss 'UTC'"), IsInline = true }
                }
            };

            return embed;
        }

        private static void AddSubscriberFieldsToStaticQueueMessageFields(PickupQueue queue, EmbedBuilder embed)
        {
            var sb = new StringBuilder();
            queue.Subscribers.ForEach(p => sb.AppendLine(p.Name));

            embed.WithFields(new EmbedFieldBuilder
            {
                Name = $"Players in queue [{queue.Subscribers.Count}/{queue.MaxInQueue}]",
                Value = queue.Subscribers.IsNullOrEmpty() ? "No players in queue" : sb.ToString(),
                IsInline = true
            });

            sb.Clear();
        }

        private static void AddWaitingListFieldsToStaticQueueMessageFields(PickupQueue queue, EmbedBuilder embed)
        {
            var sb = new StringBuilder();
            queue.WaitingList.Select((p, i) => $"{i}. {p.Name}").ToList().ForEach(p => sb.AppendLine(p));

            embed.WithFields(new EmbedFieldBuilder
            {
                Name = $"Players in waiting list [{queue.WaitingList.Count}]",
                Value = queue.WaitingList.IsNullOrEmpty() ? "No players in waiting list" : sb.ToString(),
                IsInline = true
            });

            sb.Clear();
        }
    }

    public interface IListCommandService
    {
        Task<bool> DeleteEmptyQueue(PickupQueue queue, SocketGuild guild, ISocketMessageChannel channel, bool notify);
        Task PrintTeams(PickupQueue queue, ISocketMessageChannel channel, IGuild guild);
        Task Promote(PickupQueue queue, ITextChannel pickupChannel, IGuildUser user);
        Task<PickupQueue> SaveStaticQueueMessage(PickupQueue queue, SocketGuild guild);
    }
}
