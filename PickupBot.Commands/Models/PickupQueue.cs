using System;
using System.Collections.Generic;

namespace PickupBot.Commands.Models
{
    public class PickupQueue
    {
        public PickupQueue()
        {
            Subscribers = new List<Subscriber>();
            WaitingList = new List<Subscriber>();
        }

        public ulong GuildId { get; set; }
        public string Name { get; set; }
        public string OwnerName { get; set; }
        public ulong OwnerId { get; set; }
        public int TeamSize { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public List<Subscriber> Subscribers { get; set; }
        public List<Subscriber> WaitingList { get; set; }

        public decimal Readiness => Math.Ceiling((decimal)Subscribers.Count / (TeamSize * 2) * 100);
    }
}
