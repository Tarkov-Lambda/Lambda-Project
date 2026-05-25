using System;
using System.Reflection;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using Lambda.Core;
using Cysharp.Threading.Tasks;
using System.IO;

namespace Lambda.Shared;

// Helper class for singleton refences & helper functions
public static class Debugging
{
    public static void Notify(object msg) => NotificationManagerClass.DisplayMessageNotification(msg.ToString());
    public static void NotifyLong(string msg) => NotificationManagerClass.DisplayMessageNotification(msg, EFT.Communications.ENotificationDurationType.Long);

    // #if DEBUG
    public static void Log(string msg) => LambdaPlugin.Logger.LogInfo(msg);
    // public static void LogTransaction(string msg) => Plugin.Logger.LogInfo(msg); // for stuff that goes over the wire
    public static void LogArenaController(string msg) => LambdaPlugin.Logger.LogInfo(msg);
    // public static void LogInventory(string msg) => Plugin.Logger.LogInfo(msg); // for inventory item tracking
    public static string Dump(object obj, int depth = 0, bool log = true, [CallerArgumentExpression("obj")] string name = null) => _dump(obj, depth, log, name);
    public static void DumpFile(object obj, string fileName = "Unknown Log", int depth = 0, [CallerArgumentExpression("obj")] string name = null) => _dumpFile(obj, fileName, depth);

    //     // public static void Log(string msg) => null;
    public static void LogTransaction(string msg) { }
    //     // public static void LogArenaController(string msg) { }
    public static void LogInventory(string msg) { }
    //     // public static void Dump(object obj, int depth = 0, string msg = "", [CallerArgumentExpression("obj")] string name = null) {}};
    // #else
    //         public static void Log(string msg) {}
    //         public static void LogArenaController(string msg) {}
    //         public static void LogTransaction(string msg) {}
    //         public static void LogInventory(string msg) {}
    //         public static string Dump(object obj, string msg = "", [CallerArgumentExpression("obj")] string name = null) { return ""; }
    // #endif

    private static bool _throttled;

    public static void LogError(string msg)
    {
        LambdaPlugin.Logger.LogError(msg);

        if (_throttled) return;
        _throttled = true;

        Notify("An error has occured, please check your console.");

        ResetThrottle().Forget();
    }

    private static async UniTaskVoid ResetThrottle()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(5));
        _throttled = false;
    }

    private static readonly Dictionary<string, bool> _throttledChannel = new();

    public static bool TryEnterThrottle(string key, int cooldown)
    {
        if (_throttledChannel.TryGetValue(key, out var active) && active)
            return false;

        _throttledChannel[key] = true;
        Reset(key, cooldown).Forget();
        return true;
    }

    private static async UniTaskVoid Reset(string key, int cooldown)
    {
        await UniTask.Delay(cooldown);
        _throttledChannel[key] = false;
    }

    private static void _dumpFile(object obj, string fileName = "Unnamed Log", int depth = 1)
    {
        string dump = _dump(obj, depth, false);
        var logFilePath = Path.Combine(LambdaPlugin.pathToLogs, $"{fileName}.txt");
        File.WriteAllText(logFilePath, dump);
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

    public static string Diff(object a, object b, int maxDepth = 5,
    [CallerArgumentExpression("a")] string nameA = null,
    [CallerArgumentExpression("b")] string nameB = null)
    {
        var sb = new StringBuilder();
        var visited = new HashSet<(object, object)>();

        Compare(a, b, sb, nameA ?? "A", 0, maxDepth, visited);

        return sb.Length == 0 ? "No differences." : sb.ToString();
    }

    private static void Compare(object a, object b, StringBuilder sb,
        string path, int depth, int maxDepth,
        HashSet<(object, object)> visited)
    {
        if (depth > maxDepth)
            return;

        if (ReferenceEquals(a, b))
            return;

        if (a == null || b == null)
        {
            sb.AppendLine($"{path}: {Format(a)} != {Format(b)}");
            return;
        }

        var typeA = a.GetType();
        var typeB = b.GetType();

        if (typeA != typeB)
        {
            sb.AppendLine($"{path}: Type mismatch {typeA.Name} != {typeB.Name}");
            return;
        }

        if (IsSimple(typeA))
        {
            if (!Equals(a, b))
                sb.AppendLine($"{path}: {a} != {b}");
            return;
        }

        // Prevent circular reference infinite loops
        if (visited.Contains((a, b)))
            return;

        visited.Add((a, b));

        // Handle collections
        if (typeof(IEnumerable).IsAssignableFrom(typeA) && typeA != typeof(string))
        {
            var enumA = ((IEnumerable)a).GetEnumerator();
            var enumB = ((IEnumerable)b).GetEnumerator();

            int i = 0;
            bool hasA, hasB;

            while (true)
            {
                hasA = enumA.MoveNext();
                hasB = enumB.MoveNext();

                if (!hasA && !hasB)
                    break;

                if (hasA != hasB)
                {
                    sb.AppendLine($"{path}[{i}]: Length mismatch");
                    break;
                }

                Compare(enumA.Current, enumB.Current, sb,
                    $"{path}[{i}]", depth + 1, maxDepth, visited);

                i++;
            }

            return;
        }

        // Compare fields
        var fields = typeA.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var f in fields)
        {
            var valA = f.GetValue(a);
            var valB = f.GetValue(b);

            Compare(valA, valB, sb,
                $"{path}.{f.Name}", depth + 1, maxDepth, visited);
        }
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

    private static string Format(object obj)
    {
        return obj == null ? "null" : obj.ToString();
    }
}
