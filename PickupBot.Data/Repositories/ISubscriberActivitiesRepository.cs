using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface ISubscriberActivitiesRepository
    {
        Task<SubscriberActivities> Find(IGuildUser user);
        Task<bool> Update(SubscriberActivities activities);
        Task<IEnumerable<SubscriberActivities>> List(ulong guildId);
        Task<IEnumerable<SubscriberActivities>> Top10List(ulong guildId, string propertyName);
    }
}
