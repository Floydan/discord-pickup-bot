using Discord;
using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Models
{
    public class FlaggedSubscriber : TableEntity
    {
        public FlaggedSubscriber() { }

        public FlaggedSubscriber(string guildId, string userId)
        {
            PartitionKey = guildId;
            RowKey = userId;
        }

        public FlaggedSubscriber(IGuildUser user)
        {
            PartitionKey = user.GuildId.ToString();
            RowKey = user.Id.ToString();
        }

        public string Name { get; set; }
        public string Reason { get; set; }
    }
}
