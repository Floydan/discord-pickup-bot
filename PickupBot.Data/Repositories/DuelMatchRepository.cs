using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class DuelMatchRepository : IDuelMatchRepository
    {
        private readonly IAzureTableStorage<DuelPlayer> _client;

        public DuelMatchRepository(IAzureTableStorage<DuelPlayer> client)
        {
            _client = client;
        }

        public Task<DuelPlayer> Find(IGuildUser challenger, IGuildUser challengee)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Save(IGuildUser challenger, IGuildUser challengee)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Save(DuelMatch player)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> Delete(IGuildUser challenger, IGuildUser challengee)
        {
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<DuelPlayer>> List(IGuildUser challenger, IGuildUser challengee)
        {
            throw new System.NotImplementedException();
        }
    }
}