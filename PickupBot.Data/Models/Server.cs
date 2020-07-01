using Microsoft.Azure.Cosmos.Table;

namespace PickupBot.Data.Models
{
    public class Server : TableEntity
    {
        public Server()
        {
            
        }

        public Server(ulong guildId, string host, int port)
        {
            PartitionKey = guildId.ToString();
            RowKey = host.ToLowerInvariant();

            Host = host.ToLowerInvariant();
            Port = port;
        }

        public string Host { get; set; }
        public int Port { get; set; }
        public string Continent { get; set; }
        public string ContinentCode { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string RegionName { get; set; }
        public string City { get; set; }
        public string TimeZone { get; set; }
        public int Offset { get; set; }
    }
}
