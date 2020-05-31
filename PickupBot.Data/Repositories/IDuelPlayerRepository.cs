using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IDuelPlayerRepository
    {
        Task<DuelPlayer> Find(IGuildUser user);
        Task<bool> Save(IGuildUser user, SkillLevel skillLevel);
        Task<bool> Save(DuelPlayer player);
        Task<bool> Delete(IGuildUser user);
        Task<IEnumerable<DuelPlayer>> Top10(ulong guildId);
        Task<IEnumerable<DuelPlayer>> List(IEnumerable<IGuildUser> users);
    }
}
