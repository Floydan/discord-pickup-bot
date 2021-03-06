﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories.Interfaces
{
    public interface IFlaggedSubscribersRepository
    {
        Task<FlaggedSubscriber> IsFlagged(IGuildUser user);
        Task<bool> Flag(IGuildUser user, string reason);
        Task<bool> UnFlag(IGuildUser user);
        Task<IEnumerable<FlaggedSubscriber>> List(string guildId);
    }
}
