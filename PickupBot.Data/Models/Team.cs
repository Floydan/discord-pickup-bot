using System.Collections.Generic;

namespace PickupBot.Data.Models
{
    public class Team
    {
        public string Name { get; set; }
        public List<Subscriber> Subscribers { get; set; }
        public KeyValuePair<string, ulong?> VoiceChannel { get; set; }
    }
}
