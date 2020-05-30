using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace PickupBot.Data.Models
{
    public class DuelPlayer : TableEntity
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full
        };

        public DuelPlayer() { }
        public DuelPlayer(ulong guildId, ulong userId)
        {
            PartitionKey = guildId.ToString();
            RowKey = userId.ToString();
        }

        public string Name { get; set; }
        public int Skill { get; set; }
        public bool Active { get; set; } = true;
        // ReSharper disable once InconsistentNaming
        public int MMR { get; set; } = 1000;
        
        public ulong GuildId => Convert.ToUInt64(PartitionKey);
        public ulong Id => Convert.ToUInt64(RowKey);

        public List<DuelMatch> MatchHistory { get; set; }

        public string MatchHistoryJson
        {
            get => JsonConvert.SerializeObject(MatchHistory ?? Enumerable.Empty<DuelMatch>(), Formatting.None);
            set => MatchHistory = string.IsNullOrWhiteSpace(value) ? new List<DuelMatch>() : JsonConvert.DeserializeObject<List<DuelMatch>>(value, JsonSerializerSettings);
        }
    }
}
