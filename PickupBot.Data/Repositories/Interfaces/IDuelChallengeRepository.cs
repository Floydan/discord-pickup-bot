using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories.Interfaces
{
    public interface IDuelChallengeRepository
    {
        Task<DuelChallenge> Find(IGuildUser challenger, IGuildUser challengee);
        Task<IEnumerable<DuelChallenge>> FindByChallengerId(IGuildUser challenger);
        Task<IEnumerable<DuelChallenge>> FindByChallengeeId(IGuildUser challengee);
        Task<bool> Save(IGuildUser challenger, IGuildUser challengee);
        Task<bool> Delete(DuelChallenge duelMatch);
    }
}
