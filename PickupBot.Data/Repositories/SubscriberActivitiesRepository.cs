using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class SubscriberActivitiesRepository : ISubscriberActivitiesRepository
    {
        private readonly IAzureTableStorage<SubscriberActivities> _client;

        public SubscriberActivitiesRepository(IAzureTableStorage<SubscriberActivities> client)
        {
            _client = client;
        }

        public async Task<SubscriberActivities> Find(IGuildUser user)
        {
            var result = await _client.GetItem(user.GuildId.ToString(), user.Id.ToString());
            return result ?? new SubscriberActivities(user);
        }

        public async Task<bool> Update(SubscriberActivities activities)
        {
            return await _client.InsertOrReplace(activities);
        }

        public async Task<IEnumerable<SubscriberActivities>> List(ulong guildId)
        {
            return await _client.GetList(guildId.ToString());
        }

        public async Task<IEnumerable<SubscriberActivities>> Top10List(ulong guildId, string propertyName)
        {
            return await _client.GetTopListByField(guildId.ToString(), propertyName, 10);
        }
    }
}