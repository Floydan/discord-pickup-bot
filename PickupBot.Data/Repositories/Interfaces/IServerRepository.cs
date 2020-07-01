using System.Collections.Generic;
using System.Threading.Tasks;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories.Interfaces
{
    public interface IServerRepository
    {
        Task<Server> Find(ulong guildId, string host);
        Task<bool> Save(ulong guildId, string host, int port = default);
        Task<bool> Save(Server server);
        Task<bool> Delete(Server server);
        Task<bool> Delete(ulong guildId, string host);
        Task<IEnumerable<Server>> List(ulong guildId);
    }
}
