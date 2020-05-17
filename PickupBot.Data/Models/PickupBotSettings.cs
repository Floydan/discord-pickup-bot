
// ReSharper disable InconsistentNaming

namespace PickupBot.Data.Models
{
    public class PickupBotSettings
    {
        public string DiscordToken { get; set; }
        public string RCONServerPassword { get; set; }
        public string RCONHost { get; set; }
        public string RCONPort { get; set; }
        public string CommandPrefix { get; set; }
        public string GoogleTranslateAPIKey { get; set; }
    }
}
