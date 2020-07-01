using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories.Interfaces
{
    public interface IDuelMatchRepository
    {
        Task<IEnumerable<DuelMatch>> FindByChallengerIdOrChallengeeId(IGuildUser challengee);
        Task<bool> Save(DuelMatch duelMatch);
    }
}
