﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using PickupBot.Data.Models;

namespace PickupBot.Data.Repositories
{
    public class DuelChallengeRepository : IDuelChallengeRepository
    {
        private readonly IAzureTableStorage<DuelChallenge> _client;

        public DuelChallengeRepository(IAzureTableStorage<DuelChallenge> client)
        {
            _client = client;
        }

        public async Task<DuelChallenge> Find(IGuildUser challenger, IGuildUser challengee)
        {
            return await _client.GetItem(challenger.GuildId.ToString(), $"{challenger.Id}||{challengee.Id}");
        }

        public async Task<IEnumerable<DuelChallenge>> FindByChallengerId(IGuildUser challenger)
        {
            return await _client.GetItemsPropertyEquals(challenger.GuildId.ToString(), 
                nameof(DuelChallenge.ChallengerId), 
                challenger.Id.ToString());
        }

        public async Task<IEnumerable<DuelChallenge>> FindByChallengeeId(IGuildUser challengee)
        {
            return await _client.GetItemsPropertyEquals(challengee.GuildId.ToString(), 
                nameof(DuelChallenge.ChallengeeId), 
                challengee.Id.ToString());
        }

        public async Task<bool> Save(IGuildUser challenger, IGuildUser challengee)
        {
            var duelMatch = new DuelChallenge(challenger.GuildId, challenger.Id, challengee.Id)
            {
                ChallengeDate = DateTime.UtcNow
            };

            return await _client.InsertOrReplace(duelMatch);
        }

        public async Task<bool> Save(DuelChallenge duelChallenge)
        {
            return await _client.InsertOrMerge(duelChallenge);
        }

        public async Task<bool> Delete(IGuildUser challenger, IGuildUser challengee)
        {
            return await _client.Delete(
                challenger.GuildId.ToString(),
                $"{challenger.Id}||{challengee.Id}"
            );
        }

        public async Task<bool> Delete(DuelChallenge duelChallenge)
        {
            return await _client.Delete(duelChallenge);
        }
    }
}