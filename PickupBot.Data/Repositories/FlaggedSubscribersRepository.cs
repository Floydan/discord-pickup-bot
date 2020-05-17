using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class FlaggedSubscribersRepository : IFlaggedSubscribersRepository
    {
        private readonly IAzureTableStorage<FlaggedSubscriber> _client;

        public FlaggedSubscribersRepository(IAzureTableStorage<FlaggedSubscriber> client)
        {
            _client = client;
        }

        public async Task<FlaggedSubscriber> IsFlagged(IGuildUser user)
        {
            var subscriber = await _client.GetItem(user.GuildId.ToString(), user.Id.ToString());
            return subscriber;
        }

        public async Task<bool> Flag(IGuildUser user, string reason)
        {
            return await _client.Insert(new FlaggedSubscriber(user) { Reason = reason });
        }

        public async Task<bool> UnFlag(IGuildUser user)
        {
            var flagged = await _client.GetItem(user.GuildId.ToString(), user.Id.ToString());
            if (flagged == null) return true;

            return await _client.Delete(flagged);
        }

        public async Task<IEnumerable<FlaggedSubscriber>> List(string guildId)
        {
            return await _client.GetList(guildId);
        }
    }
}
