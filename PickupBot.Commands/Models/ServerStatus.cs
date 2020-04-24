using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PickupBot.Commands.Models
{
    public class ServerStatus {
        public ServerStatus()
        {
            Players = new List<Player>();
        }

        public ServerStatus(string status) : this()
        {
            var lines = status.Split('\n');
            var regex = new Regex(@"(?<cl>(\d+))[ ]+(?<score>([-]?\d+))[ ]+(?<ping>(\d+))[ ]+(?<name>(.+))[ ]+(?<ip>(\^\d{2,4}\.\d{2,3}\.\d{2,3}\.\d{2,3}))[ ]+(?<rate>(\d+))");

            foreach (var line in lines)
            {
                if (line.StartsWith("\0")) break;
                if (line.StartsWith("????") || line.StartsWith("cl ") || line.StartsWith("-- ")) continue;
		
                if (line.StartsWith("map: "))
                    Map = line.Substring(5);
		
                var match = regex.Match(line);
                if (!match.Success) continue;

                var player = new Player {
                    CL = int.Parse(match.Groups["cl"].Value),
                    Score = int.Parse(match.Groups["score"].Value),
                    Ping = int.Parse(match.Groups["ping"].Value),
                    Name = Regex.Replace(match.Groups["name"].Value, @"\^\d", ""),
                    Rate = int.Parse(match.Groups["rate"].Value)
                };
			
                Players.Add(player);
            }
        }

        public string Map { get; set; }
        public List<Player> Players { get; set; }
    }
}
