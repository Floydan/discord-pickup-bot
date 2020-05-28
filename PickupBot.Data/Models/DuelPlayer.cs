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

        public int Wins => WonMatches.Count;
        public int Losses => LostMatches.Count;

        public List<DuelMatch> WonMatches { get; set; }
        public List<DuelMatch> LostMatches { get; set; }

        
        public string WonMatchesJson
        {
            get => JsonConvert.SerializeObject(WonMatches ?? Enumerable.Empty<DuelMatch>(), Formatting.None);
            set => WonMatches = string.IsNullOrWhiteSpace(value) ? new List<DuelMatch>() : JsonConvert.DeserializeObject<List<DuelMatch>>(value, JsonSerializerSettings);
        }
        public string LostMatchesJson
        {
            get => JsonConvert.SerializeObject(LostMatches ?? Enumerable.Empty<DuelMatch>(), Formatting.None);
            set => LostMatches = string.IsNullOrWhiteSpace(value) ? new List<DuelMatch>() : JsonConvert.DeserializeObject<List<DuelMatch>>(value, JsonSerializerSettings);
        }
    }
}
