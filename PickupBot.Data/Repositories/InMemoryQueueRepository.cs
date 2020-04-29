using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class InMemoryQueueRepository : IQueueRepository
    {
        private readonly ConcurrentDictionary<string, PickupQueue> _queueCache = new ConcurrentDictionary<string, PickupQueue>();

        public async Task<bool> AddQueue(PickupQueue queue)
        {
            var key = $"{queue.Name.ToLowerInvariant()}-{queue.GuildId}";

            if (!_queueCache.ContainsKey(key))
                return _queueCache.TryAdd(key, queue);

            return await Task.FromResult(false);
        }

        public async Task<bool> RemoveQueue(string queueName, string guildId)
        {
            var key = $"{queueName.ToLowerInvariant()}-{guildId}";

            if (!_queueCache.TryGetValue(key, out _)) return true;

            return await Task.FromResult(_queueCache.TryRemove(key, out _));
        }

        public async Task<bool> RemoveQueue(PickupQueue queue)
        {
            return await RemoveQueue(queue?.Name, queue?.GuildId);
        }

        public async Task<bool> UpdateQueue(PickupQueue queue)
        {
            var key = $"{queue.Name.ToLowerInvariant()}-{queue.GuildId}";

            _queueCache.TryGetValue(key, out var oldQueue);

            var result = _queueCache.TryUpdate(key, queue, oldQueue);

            return await Task.FromResult(result);
        }

        public async Task<PickupQueue> FindQueue(string queueName, string guildId)
        {
            var key = $"{queueName.ToLowerInvariant()}-{guildId}";

            if (!_queueCache.TryGetValue(key, out var queue)) return null;
            return await Task.FromResult(queue);
        }

        public async Task<IEnumerable<PickupQueue>> AllQueues(string guildId)
        {
            if (_queueCache == null || !_queueCache.Keys.Any())
                return await Task.FromResult(Enumerable.Empty<PickupQueue>());

            var queues = _queueCache.Values.Where(q => q.GuildId == guildId);

            return await Task.FromResult(queues);
        }
    }
}
