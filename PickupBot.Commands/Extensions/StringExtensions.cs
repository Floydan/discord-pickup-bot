using System;
using System.Collections.Generic;
using System.Text;

namespace PickupBot.Commands.Extensions
{
    public static class StringExtensions
    {
        public static string Pluralize(this string str, int amount) => amount <= 1 ? str : $"{str}s";
    }
}
