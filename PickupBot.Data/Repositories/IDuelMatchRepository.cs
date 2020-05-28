using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IDuelMatchRepository
    {
        Task<DuelPlayer> Find(IGuildUser challenger, IGuildUser challengee);
        Task<bool> Save(IGuildUser challenger, IGuildUser challengee);
        Task<bool> Save(DuelMatch player);
        Task<bool> Delete(IGuildUser challenger, IGuildUser challengee);
        Task<IEnumerable<DuelPlayer>> List(IGuildUser challenger, IGuildUser challengee);
    }
}
