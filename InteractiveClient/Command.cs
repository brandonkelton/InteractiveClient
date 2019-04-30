using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InteractiveClient
{
    internal class Command
    {
        public bool IsLocal { get; set; }
        public string Action { get; set; }
        public string[] Arguments { get; set; }

        public static bool TryParse(out Command result, string command)
        {
            string[] split = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
            {
                result = null;
                return false;
            }

            result = new Command {
                IsLocal = typeof(CommandTypes).GetFields().Any(p => p.GetRawConstantValue().ToString() == split[0].ToLower()),
                Action = split[0],
                Arguments = split.Length == 1 ? Array.Empty<string>() : split.Skip(1).ToArray()
            };

            return true;
        }

        public override string ToString()
        {
            return Action + " " + String.Join(" ", Arguments);
        }
    }
}
