using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public interface IFlaggedSubscribersRepository
    {
        Task<FlaggedSubscriber> IsFlagged(IGuildUser user);
        Task<bool> Flag(IGuildUser user, string reason);
        Task<bool> UnFlag(IGuildUser user);
        Task<IEnumerable<FlaggedSubscriber>> List(string guildId);
    }
}
