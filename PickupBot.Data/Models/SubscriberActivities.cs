using Discord;
using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Models
{
    public class SubscriberActivities : TableEntity
    {
        public SubscriberActivities()
        {
        }

        public SubscriberActivities(IGuildUser user) : this(user.GuildId, user.Id) { }

        public SubscriberActivities(ulong guildId, ulong userId) : this(guildId.ToString(), userId.ToString()) { }

        public SubscriberActivities(string guildId, string userId)
        {
            PartitionKey = guildId;
            RowKey = userId;
        }

        public int PickupAdd { get; set; }
        public int PickupCreate { get; set; }
        public int PickupPromote { get; set; }
        public int PickupTop10 { get; set; }
    }
}
