using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Commands.Models;

namespace PickupBot.Commands.Repositories
{
    public class InMemoryQueueRepository : IQueueRepository
    {
        private readonly ConcurrentDictionary<string, PickupQueue> _cache = new ConcurrentDictionary<string, PickupQueue>();

        public async Task<bool> AddQueue(PickupQueue queue)
        {
            var key = $"{queue.Name.ToLowerInvariant()}-{queue.GuildId}";

            if (!_cache.ContainsKey(key))
                return _cache.TryAdd(key, queue);

            return await Task.FromResult(false);
        }

        public async Task<bool> RemoveQueue(IUser user, string queueName, ulong guildId)
        {
            var key = $"{queueName.ToLowerInvariant()}-{guildId}";

            if (!_cache.TryGetValue(key, out var queue)) return true;

            if (queue.OwnerId == user.Id)
            {
                return _cache.TryRemove(key, out _);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> UpdateQueue(PickupQueue queue)
        {
            var key = $"{queue.Name.ToLowerInvariant()}-{queue.GuildId}";

            var result = _cache.GetOrAdd(key, queue);

            return await Task.FromResult(result != null);
        }

        public async Task<PickupQueue> FindQueue(string queueName, ulong guildId)
        {
            var key = $"{queueName.ToLowerInvariant()}-{guildId}";

            if (!_cache.TryGetValue(key, out var queue)) return null;
            return await Task.FromResult(queue);
        }

        public async Task<IEnumerable<PickupQueue>> AllQueues()
        {
            if (_cache == null || !_cache.Keys.Any())
                return await Task.FromResult(Enumerable.Empty<PickupQueue>());

            var queues = _cache.Values;

            return await Task.FromResult(queues);
        }
    }

    public interface IQueueRepository
    {
        Task<bool> AddQueue(PickupQueue queue);
        Task<bool> RemoveQueue(IUser user, string queueName, ulong guildId);
        Task<bool> UpdateQueue(PickupQueue queue);
        Task<PickupQueue> FindQueue(string queueName, ulong guildId);
        Task<IEnumerable<PickupQueue>> AllQueues();
    }
}
