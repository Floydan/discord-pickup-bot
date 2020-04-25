using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class InMemoryFlaggedSubscribersRepository : IFlaggedSubscribersRepository
    {
        private readonly ConcurrentDictionary<string, FlaggedSubscriber> _flaggedCache = new ConcurrentDictionary<string, FlaggedSubscriber>();

        public InMemoryFlaggedSubscribersRepository()
        {
        }

        public async Task<FlaggedSubscriber> IsFlagged(IGuildUser user)
        {
            var key = $"{user.Id}-{user.GuildId}";
            if (!_flaggedCache.TryGetValue(key, out var flaggedSubscriber)) return await Task.FromResult((FlaggedSubscriber)null);
            return flaggedSubscriber;
        }

        public async Task<bool> Flag(IGuildUser user, string reason)
        {
            var key = $"{user.Id}-{user.GuildId}";
            if (_flaggedCache.TryGetValue(key, out _)) return await Task.FromResult(true);

            return _flaggedCache.TryAdd(key, new FlaggedSubscriber(user) { PartitionKey = user.GuildId.ToString(), Reason = reason });
        }

        public async Task<bool> UnFlag(IGuildUser user)
        {
            var key = $"{user.Id}-{user.GuildId}";
            if (!_flaggedCache.TryGetValue(key, out _)) return await Task.FromResult(true);

            return _flaggedCache.TryRemove(key, out _);
        }

        public async Task<IEnumerable<FlaggedSubscriber>> List(string guildId)
        {
            if (_flaggedCache == null || !_flaggedCache.Keys.Any())
                return await Task.FromResult(Enumerable.Empty<FlaggedSubscriber>());

            var queues = _flaggedCache.Values.Where(q => q.PartitionKey == guildId);

            return await Task.FromResult(queues);
        }
    }
}
