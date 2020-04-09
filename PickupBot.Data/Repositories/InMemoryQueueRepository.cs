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
        private readonly ConcurrentDictionary<ulong, Subscriber> _flaggedUsersCache = new ConcurrentDictionary<ulong, Subscriber>();

        public async Task<bool> AddQueue(PickupQueue queue)
        {
            var key = $"{queue.Name.ToLowerInvariant()}-{queue.GuildId}";

            if (!_queueCache.ContainsKey(key))
                return _queueCache.TryAdd(key, queue);

            return await Task.FromResult(false);
        }

        public async Task<bool> RemoveQueue(IUser user, string queueName, string guildId)
        {
            var key = $"{queueName.ToLowerInvariant()}-{guildId}";

            if (!_queueCache.TryGetValue(key, out var queue)) return true;

            if (queue.OwnerId == user.Id.ToString())
            {
                return _queueCache.TryRemove(key, out _);
            }

            return await Task.FromResult(false);
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

        public async Task<bool> FlagUser(IUser user, string guildId)
        {
            if (user == null) return false;
            return await Task.FromResult(_flaggedUsersCache.TryAdd(user.Id, new Subscriber {Id = user.Id, Name = user.Username}));
        }

        public async Task<bool> UnFlagUser(IUser user, string guildId)
        {
            if (user == null) return false;
            return await Task.FromResult(_flaggedUsersCache.TryRemove(user.Id, out _));
        }

        public async Task<IEnumerable<Subscriber>> GetAllFlaggedUsers(string guildId)
        {
            return await Task.FromResult(_flaggedUsersCache.Values);
        }
    }
}
