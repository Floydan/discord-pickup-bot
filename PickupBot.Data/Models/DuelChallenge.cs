using System;
using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Models
{
    public class DuelChallenge : TableEntity
    {
        public DuelChallenge() { }

        public DuelChallenge(ulong guildId, ulong challengerId, ulong challengeeId) : this()
        {
            PartitionKey = guildId.ToString();
            RowKey = $"{challengerId}||{challengeeId}";
            ChallengerId = challengerId.ToString();
            ChallengeeId = challengeeId.ToString();
            ChallengeDate = DateTime.UtcNow;
        }
        
        // ReSharper disable once InconsistentNaming
        public string ChallengerId { get; set; }
        public string ChallengeeId { get; set; }
        public DateTime ChallengeDate { get; set; }
    }
}
