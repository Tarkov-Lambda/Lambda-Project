using System;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;

namespace ifp.arena.bep.Core
{
    // Helper class for singleton refences & helper functions
    public static class Debugging
    {
        public static void Notify(object msg) => NotificationManagerClass.DisplayMessageNotification(msg.ToString());
        public static void NotifyLong(string msg) => NotificationManagerClass.DisplayMessageNotification(msg, EFT.Communications.ENotificationDurationType.Long);

#if DEBUG
        public static void Log(string msg) => Plugin.Logger.LogInfo(msg);
        // public static void LogTransaction(string msg) => Plugin.Logger.LogInfo(msg); // for stuff that goes over the wire
        public static void LogArenaController(string msg) => Plugin.Logger.LogInfo(msg);
        public static void LogInventory(string msg) => Plugin.Logger.LogInfo(msg); // for inventory item tracking
        public static void Dump(object obj, int depth = 0, string msg = "", [CallerArgumentExpression("obj")] string name = null) => _dump(obj, depth, msg, name);

        // public static void Log(string msg) => null;
        public static void LogTransaction(string msg) { }
        // public static void LogArenaController(string msg) { }
        // public static void LogInventory(string msg) => null;
        // public static void Dump(object obj, int depth = 0, string msg = "", [CallerArgumentExpression("obj")] string name = null) {}};
#else 
        public static void Log(string msg) {}
        public static void LogArenaController(string msg) {}
        public static void LogTransaction(string msg) {}
        public static void LogInventory(string msg) {}
        public static void Dump(object obj, string msg = "", [CallerArgumentExpression("obj")] string name = null) { }
#endif

        public static void LogError(string msg) => Plugin.Logger.LogError(msg);

        // public static void PlayMusic(MusicEvent musicEvent) => MusicManager.Instance?.PlayEvent(musicEvent);
        // public static void PlayMusic(MusicEvent musicEvent) => D.Notify(musicEvent.ToString());

        private static void _dump(object obj, int depth = 1, string msg = "", [CallerArgumentExpression("obj")] string name = null)
        {
            if (obj == null) return;

            var sb = new StringBuilder();
            sb.Append(msg).Append("\n");

            DumpObject(obj, sb, name, 0, depth);

            D.Log(sb.ToString());
        }

        private static void DumpObject(object obj, StringBuilder sb, string name, int currentDepth, int maxDepth)
        {
            if (obj == null) return;

            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            sb.Append(name).Append(" : ").Append(type.Name).Append("\n{");

            foreach (var f in fields)
            {
                var value = f.GetValue(obj);
                sb.Append("\n   ").Append(f.Name).Append(" = ");

                if (value == null)
                {
                    sb.Append("null");
                }
                else if (IsSimple(value.GetType()) || currentDepth >= maxDepth)
                {
                    // Primitive or max depth reached → just print value
                    sb.Append(value);
                }
                else
                {
                    // Expand nested object (1 level deep)
                    sb.Append("\n   { ");
                    DumpObject(value, sb, f.Name, currentDepth + 1, maxDepth);
                    sb.Append("\n   }");
                }

                sb.Append(",");
            }

            sb.Append("\n}");
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal);
        }
    }
}