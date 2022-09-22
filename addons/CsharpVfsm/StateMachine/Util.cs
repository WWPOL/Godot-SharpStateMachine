using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GodotDictionary = Godot.Collections.Dictionary;

public static class PluginUtil
{
    /// <summary>
    /// Creates a <see cref="GodotDictionary"/> containing the necessary keys for use with an object overriding
    /// <c>_GetPropertyList</c>.
    /// </summary>
    public static GodotDictionary MakeProperty(
            string name,
            Variant.Type type,
            PropertyUsageFlags usage = PropertyUsageFlags.Default,
            PropertyHint hint = PropertyHint.None,
            string? hintString = "")
    {
        return new GodotDictionary {
            ["name"] = name,
            ["type"] = type,
            ["usage"] = usage,
            ["hint"] = hint,
            ["hint_string"] = hintString
        };
    }
    
    /// <summary>
    /// Create a hint string for the given enum type to be used when setting the <c>hint_string</c> entry of a
    /// <c>_GetPropertyList</c> result.
    /// </summary>
    /// <seealso cref="MakeProperty"/>
    public static string EnumHintString<T>()
        where T : Enum
    {
        var s = string.Empty;
        foreach (var e in Enum.GetValues(typeof(T))) {
            if (!s.Empty())
                s += ",";
            s += $"{e}:{(int)e}";
        }
        return s;
    }
}

public static class NodeExtension 
{
    public static IEnumerable<Node> GetChildNodes(this Node node)
        => node.GetChildren().Cast<Node>();
}

// Needed to prevent some dumb ass error.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}