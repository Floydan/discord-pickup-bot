using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PickupBot.Commands.Extensions;

namespace PickupBot.Commands.Infrastructure.Utilities
{
    public static class OperatorParser
    {
        private static readonly string[] AllowedOperators =
        {
            "-captains", 
            "-captain", 
            "-coop", 
            "-nocoop", 
            "-novoice",
            "-gamemode",
            "-gmode",
            "-rcon",
            "-norcon",
            "-host",
            "-port",
            "-game",
            "-teamsize"
        };

        private static readonly Regex OperatorsRegex = new Regex(@"(?<operator>-\w+)+[:]?(?<value>[^-]*)");

        public static Dictionary<string, List<string>> Parse(string operators)
        {
            if (string.IsNullOrWhiteSpace(operators)) return null;
            
            var matches = OperatorsRegex.Matches(operators);

            if (!matches.Any(m => m.Success)) return null;

            var dict = new Dictionary<string, List<string>>();
            foreach (Match match in matches)
            {
                if (!match.Success) continue;

                var op = match.Groups["operator"]?.Value.ToLowerInvariant().Trim();
                if(op == null || !AllowedOperators.Contains(op)) continue;

                var val = match.Groups["value"]?.Value
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                if (dict.ContainsKey(op))
                {
                    dict[op].AddRange(val);
                    continue;
                }

                List<string> valList = null;
                if (match.Groups["value"].Success && !val.IsNullOrEmpty())
                    valList = val.ToList();
			
                dict.Add(op, valList);
            }

            return dict;
        }
    }
}
