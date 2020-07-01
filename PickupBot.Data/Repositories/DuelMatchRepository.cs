using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;
using PickupBot.Data.Repositories.Interfaces;

namespace PickupBot.Data.Repositories
{
    public class DuelMatchRepository : IDuelMatchRepository
    {
        private readonly IAzureTableStorage<DuelMatch> _client;

        public DuelMatchRepository(IAzureTableStorage<DuelMatch> client)
        {
            _client = client;
        }

        public async Task<IEnumerable<DuelMatch>> FindByChallengerIdOrChallengeeId(IGuildUser challengee)
        {
            return await _client.GetItemsPropertyEquals(challengee.GuildId.ToString(), 
                challengee.Id.ToString(),
                nameof(DuelMatch.ChallengerId),
                nameof(DuelMatch.ChallengeeId));
        }

        public async Task<bool> Save(DuelMatch duelMatch)
        {
            return await _client.InsertOrMerge(duelMatch);
        }
    }
}