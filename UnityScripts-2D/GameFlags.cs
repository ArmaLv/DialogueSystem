using System.Collections.Generic;
using UnityEngine;

public static class GameFlags
{
    private static readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();

    public static void Set(string id, bool value = true)
    {
        _flags[id] = value;
        Debug.Log($"[GameFlags] '{id}' = {value}");
    }

    public static bool IsSet(string id)
    {
        return !string.IsNullOrEmpty(id) && _flags.TryGetValue(id, out bool v) && v;
    }

    public static void Reset() => _flags.Clear();
}
