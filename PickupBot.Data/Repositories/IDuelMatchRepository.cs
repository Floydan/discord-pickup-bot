using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IDuelMatchRepository
    {
        Task<DuelMatch> Find(IGuildUser challenger, IGuildUser challengee);
        Task<IEnumerable<DuelMatch>> FindByChallengerId(IGuildUser challenger);
        Task<IEnumerable<DuelMatch>> FindByChallengeeId(IGuildUser challengee);
        Task<bool> Save(IGuildUser challenger, IGuildUser challengee);
        Task<bool> Save(DuelMatch duelMatch);
        Task<bool> Delete(IGuildUser challenger, IGuildUser challengee);
        Task<bool> Delete(DuelMatch duelMatch);
        Task<IEnumerable<DuelMatch>> List(IGuildUser challenger, IGuildUser challengee);
    }
}
