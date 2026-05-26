using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Comfort.Common;
using EFT;

public static class PacketWardenUtils
{
    public static INetworkBackend Network           => Plugin.Network;

    public static event Action OnNetworkCreated;
    public static event Action OnNetworkDestroyed;

    public static void TriggerNetworkCreated()      => OnNetworkCreated?.Invoke();
    public static void TriggerNetworkDestroyed()    => OnNetworkDestroyed?.Invoke();

    public static GameWorld GameWorld               => Singleton<GameWorld>.Instance;
    public static Player MainPlayer                 => GetMainPlayer();

    public static bool HasGameStarted               = false;

    public static void Log(string msg)            => Plugin.Logger.LogInfo(msg);
    public static void Notify(object msg)           => NotificationManagerClass.DisplayMessageNotification(msg.ToString());
    public static string Dump(object obj, int depth = 0, bool log = true, [CallerArgumentExpression("obj")] string name = null) => _dump(obj, depth, log, name);

    // bro thinks he's the main character
    private static Player GetMainPlayer()
    {
        try
        {
            if (Plugin.Network.IsHeadless)
            {
                Log("Headless trying to access MainPlayer. This is not supposed to happen.");
                Log(Environment.StackTrace);
                return null;
            }
            return IsInRaid() ? GameWorld.MainPlayer : null;
        }
        catch (Exception ex)
        {
            Dump(ex);
            Log(ex.StackTrace);
        }

        return null;
    }

    public static bool IsInRaid()
    {
        return GameWorld != null && GameWorld is not HideoutGameWorld;
    }

    private static string _dump(object obj, int depth = 1, bool log = true, [CallerArgumentExpression("obj")] string name = null)
    {
        if (obj == null) return "";

        var sb = new StringBuilder();

        DumpObject(obj, sb, name, 0, depth);

        if (log)
        {
            Log(sb.ToString());
        }

        return sb.ToString();
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
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(Guid);
    }
}