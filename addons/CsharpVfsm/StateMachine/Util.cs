using System.Collections.Generic;
using System.Linq;
using Godot;

public static class PluginUtil
{
    [Signal] public delegate void ChildChanged(); 
}

public static class NodeExtension 
{
    public static IEnumerable<Node> GetChildNodes(this Node node)
        => node.GetChildren().Cast<Node>();
}

/// Needed to prevent some dumb ass error.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit {}
}