using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using PacketWarden;

namespace Lambda.Core.Networking.Commands
{
    public static class ChatCommandInterceptor
    {
        private class CommandMethodInfo
        {
            public MethodInfo Method { get; set; }
            public ChatCommandAttribute Attribute { get; set; }
            public ParameterInfo[] Parameters { get; set; }
        }

        private static readonly Dictionary<string, CommandMethodInfo> Commands = new();

        public static void Initialize()
        {
            var methods = Assembly.GetExecutingAssembly()
                .GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<ChatCommandAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<ChatCommandAttribute>();
                Commands[attr.Name] = new CommandMethodInfo
                {
                    Method = method,
                    Attribute = attr,
                    Parameters = method.GetParameters().Skip(1).ToArray() // skip CommandContext
                };
            }
            D.Log($"[Lambda Commands] Registered {Commands.Count} chat commands.");
        }

        public static bool TryHandleLocal(string message)
        {
            if (!message.StartsWith("!")) return false;

            var (cmdName, args) = ParseRawMessage(message);
            if (!Commands.TryGetValue(cmdName, out var cmdInfo)) return false; // unknown command, let server handle it

            if (cmdInfo.Attribute.Target == CommandTarget.ClientOnly)
            {
                ExecuteCommand(cmdInfo, H.MainPlayer, message, args);
                return true; // consumed
            }

            return false; // server command, send the packet
        }

        public static void HandleServer(Player sender, string message)
        {
            var (cmdName, args) = ParseRawMessage(message);
            
            if (!Commands.TryGetValue(cmdName, out var cmdInfo))
            {
                Singleton<ServerMessagePacketWarden>.Instance.SendToPlayer(sender, "Unknown command.");
                return;
            }

            if (cmdInfo.Attribute.Target != CommandTarget.ServerOnly) return;

            // authority check
            if (cmdInfo.Attribute.Authority == PacketAuthority.Admin)
            {
                var score = H.GetPlayerContext(sender);
                if (score == null || !score.IsAdmin)
                {
                    Singleton<ServerMessagePacketWarden>.Instance.SendToPlayer(sender, "You do not have permission to use this command.");
                    return;
                }
            }

            ExecuteCommand(cmdInfo, sender, message, args);
        }

        private static void ExecuteCommand(CommandMethodInfo cmdInfo, Player sender, string rawMessage, string[] args)
        {
            var context = new CommandContext(sender, rawMessage, args);

            try
            {
                if (args.Length < cmdInfo.Parameters.Length)
                    throw new ArgumentException("Not enough arguments provided.");

                // map strings to method parameters dynamically
                var methodArgs = new object[cmdInfo.Parameters.Length + 1];
                methodArgs[0] = context; // first param is always CommandContext

                for (int i = 0; i < cmdInfo.Parameters.Length; i++)
                {
                    // If it's the last parameter and it's a string, consume the rest of the message
                    if (i == cmdInfo.Parameters.Length - 1 && cmdInfo.Parameters[i].ParameterType == typeof(string))
                    {
                        methodArgs[i + 1] = string.Join(" ", args.Skip(i));
                    }
                    else
                    {
                        methodArgs[i + 1] = CommandArgumentParsers.Parse(args[i], cmdInfo.Parameters[i].ParameterType);
                    }
                }

                cmdInfo.Method.Invoke(null, methodArgs);
            }
            catch (ArgumentException ex)
            {
                context.Reply($"[Error] {ex.Message}");
            }
            catch (Exception ex)
            {
                context.Reply("[Error] Command execution failed.");
                D.LogError($"Command Execution Error: {ex}");
            }
        }

        private static (string name, string[] args) ParseRawMessage(string message)
        {
            // strings in quotes are not supported
            var parts = message.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return (parts[0].ToLowerInvariant(), parts.Skip(1).ToArray());
        }
    }
}