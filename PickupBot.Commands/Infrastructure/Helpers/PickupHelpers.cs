using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        
        public static string ParseSubscribers(PickupQueue queue)
        {
            var subscribers = queue.Subscribers.Select(w => w.Name).ToList();
            if ((queue.MaxInQueue) - queue.Subscribers.Count > 0)
                subscribers.AddRange(Enumerable.Repeat("[?]", (queue.MaxInQueue) - queue.Subscribers.Count));

            //if queue found and user is in queue
            return String.Join(", ", subscribers);
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
