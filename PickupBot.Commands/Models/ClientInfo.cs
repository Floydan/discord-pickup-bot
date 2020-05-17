using System;
using System.Data;
using System.Text.RegularExpressions;
using PickupBot.Commands.Utilities;

namespace PickupBot.Commands.Models
{
    public class ClientInfo
    {
        public ClientInfo(string data)
        {
            var lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("name"))
                    ExactName = line.Replace("name", "").Trim();
                else if(line.StartsWith("rate"))
                    Rate = int.Parse(line.Replace("rate", "").Trim());
                else if(line.StartsWith("snaps"))
                    Snaps = int.Parse(line.Replace("snaps", "").Trim());
                else if(line.StartsWith("handicap"))
                    Handicap = int.Parse(line.Replace("handicap", "").Trim());
                else if(line.StartsWith("cl_timeNudge"))
                    TimeNudge = int.Parse(line.Replace("cl_timeNudge", "").Trim());
                else if (line.StartsWith("DiscordId", StringComparison.OrdinalIgnoreCase))
                    DiscordId = line.Replace("DiscordId", "", StringComparison.OrdinalIgnoreCase).Trim();
                else if (line.StartsWith("cl_guid"))
                    Guid = line.Replace("cl_guid", "").Trim();
            }
        }

        public string ToTable()
        {
            var dataTable = new DataTable();
            dataTable.Columns.Add("Key", typeof(string));
            dataTable.Columns.Add("Value", typeof(string));

            var row = dataTable.NewRow();
            row[0] = nameof(Name);
            row[1] = Name;
            dataTable.Rows.Add(row);
            
            row = dataTable.NewRow();
            row[0] = nameof(ExactName);
            row[1] = ExactName;
            dataTable.Rows.Add(row);
            
            row = dataTable.NewRow();
            row[0] = nameof(Rate);
            row[1] = Rate.ToString();
            dataTable.Rows.Add(row);

            row = dataTable.NewRow();
            row[0] = nameof(Snaps);
            row[1] = Snaps.ToString();
            dataTable.Rows.Add(row);

            row = dataTable.NewRow();
            row[0] = nameof(Handicap);
            row[1] = Handicap.ToString();
            dataTable.Rows.Add(row);

            row = dataTable.NewRow();
            row[0] = nameof(TimeNudge);
            row[1] = TimeNudge.ToString();
            dataTable.Rows.Add(row);

            row = dataTable.NewRow();
            row[0] = nameof(DiscordId);
            row[1] = DiscordId;
            dataTable.Rows.Add(row);

            row = dataTable.NewRow();
            row[0] = nameof(Guid);
            row[1] = Guid;
            dataTable.Rows.Add(row);

            return AsciiTableGenerator.CreateAsciiTableFromDataTable(dataTable)?.ToString();
        }

        public string Name => Regex.Replace(ExactName, @"\^\d{1}", "");
        public string ExactName { get; }
        public int Rate { get; }
        public int Snaps { get; }
        public int Handicap { get; }
        public int TimeNudge { get; }
        public string DiscordId { get; }
        public string Guid { get; }
    }
}
