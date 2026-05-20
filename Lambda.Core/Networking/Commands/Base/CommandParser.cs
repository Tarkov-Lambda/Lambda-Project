using System;
using System.Collections.Generic;
using System.Linq;
using EFT;

namespace Lambda.Core.Networking.Commands
{
    public static class CommandArgumentParsers
    {
        // Maps a Type to a function that converts a string to that Type
        private static Dictionary<Type, Func<string, object>> Parsers = new()
        {
            { typeof(string), s => s },
            { typeof(int), s => int.TryParse(s, out int i) ? i : throw new ArgumentException($"'{s}' is not a valid integer.") },
            { typeof(float), s => float.TryParse(s, out float f) ? f : throw new ArgumentException($"'{s}' is not a valid float.") },
            { typeof(bool), s => bool.TryParse(s, out bool b) ? b : throw new ArgumentException($"'{s}' is not a valid boolean.") },
            
            { typeof(Player), s => ResolvePlayer(s) },
            { typeof(Faction), s => Enum.TryParse(s, true, out Faction f) ? f : throw new ArgumentException($"'{s}' is not a valid Faction.") }
        };

        public static void AddOrUpdateParser(Type type, Func<string, object> parser)
        {
            Parsers[type] = parser;
            D.Log($"[Lambda Commands] Registered a parser for {type.FullName}");
        }

        public static object Parse(string arg, Type targetType)
        {
            if (Parsers.TryGetValue(targetType, out var parser))
                return parser(arg);

            throw new NotSupportedException($"Type {targetType.Name} is not supported in commands.");
        }

        private static Player ResolvePlayer(string identifier)
        {
            var player = H.GetPlayerByName(identifier);

            if (player == null) throw new ArgumentException($"Player '{identifier}' not found.");
            return player;
        }
    }
}