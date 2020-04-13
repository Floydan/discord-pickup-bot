using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace PickupBot.Data.Models
{
    public class PickupQueue : TableEntity
    {
        public PickupQueue() { }

        public PickupQueue(string guildId, string name)
        {
            Subscribers = new List<Subscriber>();
            WaitingList = new List<Subscriber>();
            PartitionKey = guildId;
            RowKey = name.ToLowerInvariant();
        }

        public string GuildId { get; set; }
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public string OwnerId { get; set; }
        public int TeamSize { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string SubscribersJson
        {
            get => JsonConvert.SerializeObject(Subscribers ?? Enumerable.Empty<Subscriber>(), Formatting.None);
            set => Subscribers = string.IsNullOrWhiteSpace(value) ? new List<Subscriber>() : JsonConvert.DeserializeObject<List<Subscriber>>(value, new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.Auto,
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full
                });
        }
        public string WaitingListJson
        {
            get => JsonConvert.SerializeObject(WaitingList ?? Enumerable.Empty<Subscriber>(), Formatting.None);
            set => WaitingList = string.IsNullOrWhiteSpace(value) ? new List<Subscriber>() : JsonConvert.DeserializeObject<List<Subscriber>>(value, new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.Auto,
                    TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full
                });
        }
        public List<Subscriber> Subscribers { get; set; }
        public List<Subscriber> WaitingList { get; private set; }

        public decimal Readiness => Math.Ceiling((decimal)Subscribers.Count / MaxInQueue * 100);
        public int MaxInQueue => TeamSize * 2;
    }
}
