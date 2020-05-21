using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using PickupBot.Commands.Utilities;

namespace PickupBot.Commands.Models
{
    public class ServerStatus
    {
        private static readonly Regex LineParserRegex =
            new Regex(
                @"(?<cl>(\d+))[ ]+(?<score>([-]?\d+))[ ]+(?<ping>(\d+))[ ]+(?<name>(.+))[ ]+(?<ip>(\^\d{2,4}\.\d{1,3}\.\d{1,3}\.\d{1,3}))[ ]+(?<rate>(\d+))");

        public ServerStatus()
        {
            Players = new List<Player>();
        }

        public ServerStatus(string status) : this()
        {
            if(string.IsNullOrEmpty(status))
                throw new ArgumentException($"Argument can't be null '{nameof(status)}'");
            
            var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim());

            foreach (var line in lines)
            {
                if (line.StartsWith("????") || line.StartsWith("cl ") || line.StartsWith("-- ")) continue;
		
                if (line.StartsWith("map: "))
                    Map = line.Substring(5);
		
                var match = LineParserRegex.Match(line);
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

        public string PlayersToTable()
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Score", typeof(int));
            dataTable.Columns.Add("Ping", typeof(int));

            foreach (var player in Players)
            {
                var row = dataTable.NewRow();
                row[0] = player.Name;
                row[1] = player.Score;
                row[2] = player.Ping;

                dataTable.Rows.Add(row);
            }

            return AsciiTableGenerator.CreateAsciiTableFromDataTable(dataTable)?.ToString();
        }

        public string Map { get; set; }
        public List<Player> Players { get; set; }
    }
}
