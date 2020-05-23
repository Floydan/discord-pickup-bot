using System.Collections.Generic;
using System.Threading.Tasks;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IQueueRepository
    {
        Task<bool> AddQueue(PickupQueue queue);
        Task<bool> RemoveQueue(string queueName, string guildId);
        Task<bool> RemoveQueue(PickupQueue queue);
        Task<bool> UpdateQueue(PickupQueue queue);
        Task<PickupQueue> FindQueue(string queueName, string guildId);
        Task<PickupQueue> FindQueueByMessageId(ulong messageId, string guildId);
        Task<IEnumerable<PickupQueue>> AllQueues(string guildId);

    }
}
