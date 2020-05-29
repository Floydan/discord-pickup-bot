using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class DuelMatchRepository : IDuelMatchRepository
    {
        private readonly IAzureTableStorage<DuelMatch> _client;

        public DuelMatchRepository(IAzureTableStorage<DuelMatch> client)
        {
            _client = client;
        }

        public async Task<DuelMatch> Find(IGuildUser challenger, IGuildUser challengee)
        {
            return await _client.GetItem(challenger.GuildId.ToString(), $"{challenger.Id}||{challengee.Id}");
        }

        public async Task<IEnumerable<DuelMatch>> FindByChallengerId(IGuildUser challenger)
        {
            return await _client.GetItemsPropertyEquals(challenger.GuildId.ToString(), 
                nameof(DuelMatch.ChallengerId), 
                challenger.Id.ToString());
        }

        public async Task<IEnumerable<DuelMatch>> FindByChallengeeId(IGuildUser challengee)
        {
            return await _client.GetItemsPropertyEquals(challengee.GuildId.ToString(), 
                nameof(DuelMatch.ChallengeeId), 
                challengee.Id.ToString());
        }

        public async Task<bool> Save(IGuildUser challenger, IGuildUser challengee)
        {
            var duelMatch = new DuelMatch(challenger.GuildId, challenger.Id, challengee.Id)
            {
                ChallengeDate = DateTime.UtcNow
            };

            return await _client.InsertOrReplace(duelMatch);
        }

        public async Task<bool> Save(DuelMatch duelMatch)
        {
            return await _client.InsertOrMerge(duelMatch);
        }

        public async Task<bool> Delete(IGuildUser challenger, IGuildUser challengee)
        {
            return await _client.Delete(
                challenger.GuildId.ToString(),
                $"{challenger.Id}||{challengee.Id}"
            );
        }

        public async Task<bool> Delete(DuelMatch duelMatch)
        {
            return await _client.Delete(duelMatch);
        }

        public Task<IEnumerable<DuelMatch>> List(IGuildUser challenger, IGuildUser challengee)
        {
            throw new System.NotImplementedException();
        }
    }
}