using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using PickupBot.Data.Models;

namespace PickupBot.Commands.Infrastructure.Helpers
{
    public static class PickupHelpers
    {
        public static async Task<IVoiceChannel> GetOrCreateVoiceChannel(string name, ulong categoryId, SocketGuild guild)
        {
            return (IVoiceChannel)guild.VoiceChannels.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                   ?? await guild.CreateVoiceChannelAsync(name, properties => properties.CategoryId = categoryId);
        }

        public static bool IsInPickupChannel(IChannel channel) => channel.Name.StartsWith("pickup", StringComparison.OrdinalIgnoreCase);
        public static bool IsInDuelChannel(IChannel channel) => channel.Name.Equals("duel", StringComparison.OrdinalIgnoreCase);

        public static async Task<ITextChannel> GetPickupQueuesChannel(SocketGuild guild)
        {
            var queuesChannel = (ITextChannel)guild.TextChannels.FirstOrDefault(c =>
                                    c.Name.Equals("active-pickups", StringComparison.OrdinalIgnoreCase)) ??
                                await guild.CreateTextChannelAsync("active-pickups",
                                    properties => { properties.Topic = "Active pickups, use reactions to signup"; });
            return queuesChannel;
        }

        public static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => w.Name).ToList();
            if ((queue.MaxInQueue) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.MaxInQueue) - queue.Subscribers.Count));

            //if queue found and user is in queue
            return string.Join(", ", subscribers);
        }

        public static async Task NotifyUsers(PickupQueue queue, string serverName, IUser guildUser, params SocketGuildUser[] users)
        {
            var usersList = string.Join(Environment.NewLine, queue.Subscribers.Where(u => u.Id != guildUser.Id).Select(u => $@"  - {u.Name}"));
            var header = $"**Contact your teammates on the \"{serverName}\" server and glhf!**";
            var remember = $"**Remember** {Environment.NewLine}" +
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
                try
                {
                    await user.SendMessageAsync(embed: embed);
                    await Task.Delay(500);
                }
                catch (Exception)
                {
                    //_logger.LogError($"Failed to send DM to {PickupHelpers.GetNickname(user)}", ex);
                }
            }
        }

        public static string GetNickname(IUser user) =>
            user switch
            {
                IGuildUser guildUser => guildUser.Nickname ?? guildUser.Username,
                IGroupUser groupUser => groupUser.Username,
                ISelfUser selfUser => selfUser.Username,
                _ => user.Username
            };

        public static string GetMention(IMentionable user) =>
            user switch
            {
                IGuildUser guildUser => guildUser.Mention,
                IGroupUser groupUser => groupUser.Mention,
                ISelfUser selfUser => selfUser.Mention,
                _ => user.Mention
            };
    }
}
