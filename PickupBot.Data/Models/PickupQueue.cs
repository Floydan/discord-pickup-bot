using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace PickupBot.Data.Models
{
    public class PickupQueue : TableEntity
    {
        private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full
        };

        public PickupQueue() { }

        public PickupQueue(string guildId, string name)
        {
            Subscribers = new List<Subscriber>();
            WaitingList = new List<Subscriber>();
            PartitionKey = guildId;
            RowKey = name.ToLowerInvariant();
            GuildId = guildId;
            Name = name;
        }

        public string GuildId { get; set; }
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public string OwnerId { get; set; }
        public int TeamSize { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public bool IsCoop { get; set; }
        public bool Rcon { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public bool Started { get; set; }

        public string SubscribersJson
        {
            get => JsonConvert.SerializeObject(Subscribers ?? Enumerable.Empty<Subscriber>(), Formatting.None);
            set => Subscribers = string.IsNullOrWhiteSpace(value) ? new List<Subscriber>() : JsonConvert.DeserializeObject<List<Subscriber>>(value, _jsonSerializerSettings);
        }
        public string WaitingListJson
        {
            get => JsonConvert.SerializeObject(WaitingList ?? Enumerable.Empty<Subscriber>(), Formatting.None);
            set => WaitingList = string.IsNullOrWhiteSpace(value) ? new List<Subscriber>() : JsonConvert.DeserializeObject<List<Subscriber>>(value, _jsonSerializerSettings);
        }
        public string GamesListJson
        {
            get => JsonConvert.SerializeObject(Games ?? Enumerable.Empty<string>(), Formatting.None);
            set => Games = string.IsNullOrWhiteSpace(value) ? Enumerable.Empty<string>() : JsonConvert.DeserializeObject<IEnumerable<string>>(value, _jsonSerializerSettings);
        }
        public string TeamsJson
        {
            get => JsonConvert.SerializeObject(Teams ?? Enumerable.Empty<Team>(), Formatting.None);
            set => Teams = string.IsNullOrWhiteSpace(value) ? new List<Team>() : JsonConvert.DeserializeObject<List<Team>>(value, _jsonSerializerSettings);
        }

        public List<Subscriber> Subscribers { get; set; }
        public List<Subscriber> WaitingList { get; set; }
        public IEnumerable<string> Games { get; set; }
        public List<Team> Teams { get; set; }

        public decimal Readiness => Math.Ceiling((decimal)Subscribers.Count / MaxInQueue * 100);
        public int MaxInQueue => IsCoop ? TeamSize : TeamSize * 2;
    }
}
