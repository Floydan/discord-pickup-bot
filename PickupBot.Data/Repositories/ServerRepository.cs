using System.Collections.Generic;
using System.Threading.Tasks;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;

namespace PickupBot.Data.Repositories
{
    public class ServerRepository : IServerRepository
    {
        private readonly IAzureTableStorage<Server> _client;

        public ServerRepository(IAzureTableStorage<Server> client)
        {
            _client = client;
        }

        public async Task<Server> Find(ulong guildId, string host)
        {
            return await _client.GetItem(guildId.ToString(), host.ToLowerInvariant());
        }

        public async Task<bool> Save(ulong guildId, string host, int port = default)
        {
            return await _client.Insert(new Server(guildId, host, port));
        }

        public async Task<bool> Save(Server server)
        {
            return await _client.InsertOrMerge(server);
        }

        public async Task<bool> Delete(Server server)
        {
            if (server == null) return false;
            if (string.IsNullOrWhiteSpace(server.PartitionKey) || string.IsNullOrWhiteSpace(server.RowKey)) return false;
            var result = await _client.Delete(server);
            return result;
        }

        public async Task<bool> Delete(ulong guildId, string host)
        {
            return await _client.Delete(guildId.ToString(), host.ToLowerInvariant());
        }

        public async Task<IEnumerable<Server>> List(ulong guildId)
        {
            return await _client.GetList(guildId.ToString());
        }
    }
}