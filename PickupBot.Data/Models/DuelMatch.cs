using System;
using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Models
{
    public class DuelMatch : TableEntity
    {
        public DuelMatch() { }

        public DuelMatch(ulong guildId, ulong challengerId, ulong challengeeId) : this()
        {
            PartitionKey = guildId.ToString();
            RowKey = $"{challengerId}||{challengeeId}";
            ChallengerId = challengerId.ToString();
            ChallengeeId = challengeeId.ToString();
            ChallengeDate = DateTime.UtcNow;
        }

        public string ChallengerId { get; set; }
        public string ChallengeeId { get; set; }

        public string WinnerId { get; set; }
        public string WinnerName { get; set; }

        public string LooserId { get; set; }
        public string LooserName { get; set; }

        public DateTime MatchDate { get; set; }
        public DateTime ChallengeDate { get; set; }
        public bool Started { get; set; }
    }
}
