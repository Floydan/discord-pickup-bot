using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IQueueRepository
    {
        Task<bool> AddQueue(PickupQueue queue);
        Task<bool> RemoveQueue(IUser user, string queueName, string guildId);
        Task<bool> UpdateQueue(PickupQueue queue);
        Task<PickupQueue> FindQueue(string queueName, string guildId);
        Task<IEnumerable<PickupQueue>> AllQueues(string guildId);
        Task<bool> FlagUser(IUser user, string guildId);
        Task<bool> UnFlagUser(IUser user, string guildId);
        Task<IEnumerable<Subscriber>> GetAllFlaggedUsers(string guildId);

    }
}
