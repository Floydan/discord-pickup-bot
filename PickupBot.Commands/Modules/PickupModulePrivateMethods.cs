using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using PickupBot.Commands.Extensions;
using PickupBot.Commands.Infrastructure.Helpers;
using PickupBot.Commands.Infrastructure.Utilities;
using PickupBot.Data.Models;

namespace PickupBot.Commands.Modules
{
    public partial class PickupModule
    {

        private async Task<PickupQueue> LeaveInternal(PickupQueue queue, ISocketMessageChannel channel, IGuildUser user, bool notify = true)
        {
            var guild = (SocketGuild)user.Guild;
            var subscriber = queue.Subscribers.FirstOrDefault(s => s.Id == user.Id);
            if (subscriber != null)
            {
                queue.Updated = DateTime.UtcNow;
                queue.Subscribers.Remove(subscriber);

                await MoveUserFromWaitingListToSubscribers(queue, subscriber, channel, guild);

                if (!queue.Subscribers.Any() && !queue.WaitingList.Any())
                {
                    notify = await DeleteEmptyQueue(queue, guild, channel, notify);
                }
                else
                {
                    queue = await SaveStaticQueueMessage(queue, guild);
                    await _queueRepository.UpdateQueue(queue);
                }
            }

            if (notify)
                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`").AutoRemoveMessage(10);

            return queue;
        }

        private async Task MoveUserFromWaitingListToSubscribers(PickupQueue queue, Subscriber subscriber, ISocketMessageChannel channel, SocketGuild guild)
        {
            if (queue.WaitingList.Any())
            {
                var next = queue.WaitingList.First();
                queue.WaitingList.RemoveAt(0);

                queue.Subscribers.Add(next);

                var nextUser = guild.GetUser(next.Id);
                if (nextUser != null)
                {
                    await channel.SendMessageAsync($"{PickupHelpers.GetMention(nextUser)} - you have been added to '{queue.Name}' since {subscriber.Name} has left.").AutoRemoveMessage();
                    if (queue.Started)
                    {
                        var team = queue.Teams.FirstOrDefault(w => w.Subscribers.Exists(s => s.Id == subscriber.Id));
                        if (team != null)
                        {
                            team.Subscribers.Remove(team.Subscribers.Find(s => s.Id == subscriber.Id));
                            team.Subscribers.Add(new Subscriber { Name = PickupHelpers.GetNickname(nextUser), Id = nextUser.Id });
                            await ReplyAsync($"{PickupHelpers.GetMention(nextUser)} - you are on the {team.Name}").AutoRemoveMessage();
                        }
                    }

                    await PickupHelpers.NotifyUsers(queue, guild.Name, nextUser);
                }
            }
        }

        private async Task<bool> DeleteEmptyQueue(PickupQueue queue, SocketGuild guild, ISocketMessageChannel channel, bool notify)
        {
            var result = await _queueRepository.RemoveQueue(queue.Name, queue.GuildId); //Try to remove queue if its empty
            if (result)
            {
                var queuesChannel = await PickupHelpers.GetPickupQueuesChannel(guild);
                if (!string.IsNullOrEmpty(queue.StaticMessageId))
                    await queuesChannel.DeleteMessageAsync(Convert.ToUInt64(queue.StaticMessageId));
            }

            if (!notify) return false;

            await channel.SendMessageAsync($"`{queue.Name} has been removed since everyone left.`").AutoRemoveMessage(10);

            return false;
        }

        private async Task AddInternal(string queueName, SocketGuild guild, ISocketMessageChannel channel, IGuildUser user)
        {
            PickupQueue queue;
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                queue = await _queueRepository.FindQueue(queueName, guild.Id.ToString());
            }
            else
            {
                var queues = (await _queueRepository.AllQueues(guild.Id.ToString())).OrderByDescending(w => w.Readiness).ToList();
                queue = queues.Any(q => q.Readiness < 100) ? queues.FirstOrDefault(q => q.Readiness < 100) : queues.FirstOrDefault();
            }

            if (queue == null)
            {
                await channel.SendMessageAsync($"`Queue with the name '{queueName}' doesn't exists!`").AutoRemoveMessage(10);
                return;
            }

            if (!await VerifyUserFlaggedStatus(user))
                return;

            if (queue.Subscribers.Any(w => w.Id == user.Id))
            {
                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`");
                return;
            }

            var activity = await _activitiesRepository.Find(user);
            activity.PickupAdd += 1;
            
            queue.Updated = DateTime.UtcNow;
            queue.Subscribers.Add(new Subscriber { Id = user.Id, Name = PickupHelpers.GetNickname(user) });

            if (queue.Subscribers.Count >= queue.MaxInQueue)
            {
                if (queue.WaitingList.All(w => w.Id != user.Id))
                {
                    queue = await SaveStaticQueueMessage(queue, guild);

                    await channel.SendMessageAsync($"`You have been added to the '{queue.Name}' waiting list`").AutoRemoveMessage(10);
                }
                else
                {
                    await channel.SendMessageAsync($"`You are already on the '{queue.Name}' waiting list`").AutoRemoveMessage(10);
                }
            }
            else
            {
                queue = await SaveStaticQueueMessage(queue, guild);

                if (queue.Subscribers.Count == queue.MaxInQueue)
                {
                    await PickupHelpers.NotifyUsers(
                        queue, 
                        guild.Name, 
                        user, 
                        queue.Subscribers.Select(s => guild.GetUser(s.Id)).Where(u => u != null).ToArray());
                }

                await channel.SendMessageAsync($"`{queue.Name} - {PickupHelpers.ParseSubscribers(queue)}`");
            }
            
            await _queueRepository.UpdateQueue(queue);
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
            return await VerifyUserFlaggedStatus((IGuildUser)Context.User);
        }

        private async Task<bool> VerifyUserFlaggedStatus(IGuildUser user)
        {
            var flagged = await _flagRepository.IsFlagged(user);
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

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
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
                _logger.LogError(e, e.Message);
            }
        }

        private static async Task<PickupQueue> SaveStaticQueueMessage(PickupQueue queue, SocketGuild guild)
        {
            var queuesChannel = await PickupHelpers.GetPickupQueuesChannel(guild);

            //var denyAll = OverwritePermissions.DenyAll(queuesChannel);
            //await queuesChannel.AddPermissionOverwriteAsync(Context.Guild.EveryoneRole, denyAll);

            var user = guild.GetUser(Convert.ToUInt64(queue.OwnerId));

            var embed = CreateStaticQueueMessageEmbed(queue, user);

            AddSubscriberFieldsToStaticQueueMessageFields(queue, embed);
            AddWaitingListFieldsToStaticQueueMessageFields(queue, embed);

            if (string.IsNullOrEmpty(queue.StaticMessageId))
            {
                var message = await queuesChannel.SendMessageAsync(embed: embed.Build());
                await message.AddReactionsAsync(new IEmote[] { new Emoji("\u2705") }); // timer , new Emoji("\u23F2")

                queue.StaticMessageId = message.Id.ToString();
            }
            else
            {
                if (await queuesChannel.GetMessageAsync(Convert.ToUInt64(queue.StaticMessageId)) is IUserMessage message)
                    await message.ModifyAsync(m => { m.Embed = embed.Build(); });
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
}
