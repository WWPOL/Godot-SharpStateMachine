using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

using Godot;

using static CsharpVfsmPlugin;

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

    private static readonly HashSet<Type> ActionTypes = new() {
        typeof(Action), typeof(Action<>), typeof(Action<,>), typeof(Action<,,>), typeof(Action<,,,>),
        typeof(Action<,,,,>), typeof(Action<,,,,,>), typeof(Action<,,,,,,>)
    };

    private static readonly HashSet<Type> FunctionTypes = new() {
        typeof(Func<>), typeof(Func<,>), typeof(Func<,,>), typeof(Func<,,,>),
        typeof(Func<,,,,>), typeof(Func<,,,,,>), typeof(Func<,,,,,,>)
    };

    private static bool TypeIsAction(Type t)
        => ActionTypes.Contains(t) || (t.IsGenericType && ActionTypes.Contains(t.GetGenericTypeDefinition()));

    private static bool TypeIsFunc(Type t)
        => FunctionTypes.Contains(t) || (t.IsGenericType && FunctionTypes.Contains(t.GetGenericTypeDefinition()));

    /// <summary>
    /// Attempt to retrieve a function reference to the method with the given <c>methodName</c> in the given
    /// <c>targetNode</c>.
    /// </summary>
    /// <returns>
    /// The delegate for the method, or <c>null</c> if the editor is currently running. This is to avoid throwing
    /// unnecessary exceptions when using a <c>Tool</c> class in the editor. This function will not return <c>null</c>
    /// during application runtime.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The method is not found or has invalid parameters.
    /// </exception>
    public static T? GetMethodDelegateForNode<T>(Node targetNode, string methodName)
        where T : Delegate
    {
        if (targetNode is null) {
            throw new ArgumentNullException(nameof(targetNode));
        }
        
        if (!TypeIsAction(typeof(T)) && !TypeIsFunc(typeof(T))) {
            throw new InvalidOperationException("The delegate being created must be of type Action or Func");
        }
        
        var method = targetNode.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == methodName);
        
        if (method is null) {
            // Method with given name not found
            if (Engine.EditorHint) {
                // If the editor is running, we do not any information on non-Tool types. We don't want to throw
                // errors when there isn't actually a problem, so let's just let the caller deal with it.
                return null;
            }

            throw new ArgumentException($"Unable to find method \"{methodName}\" in node \"{targetNode.Name}\"");
        }

        if (TypeIsAction(typeof(T))) {
            var generics = typeof(T).GetGenericArguments();
            if (method.GetParameters().Count() != generics.Count()
                || method.GetParameters()
                    .Zip(generics, (p, t) => (p, t))
                    .Any(pair => pair.p.ParameterType != pair.t)) {
                // Arguments don't match
                throw new ArgumentException($"Invalid parameters for method \"{methodName}\"");
            }
        } else if (TypeIsFunc(typeof(T))) {
            var generics = typeof(T).GetGenericArguments();
            if (generics.Any()) {
                var ret = generics[0];
                var args = generics.Skip(1).ToList();

                if (method.ReturnType != ret) {
                    throw new ArgumentException($"Invalid return type for method \"{methodName}\"");
                }

                if (method.GetParameters()
                    .Zip(args, (p, t) => (p, t))
                    .Any(pair => pair.p.ParameterType != pair.t)) {
                    throw new ArgumentException($"Invalid parameter types for method \"{methodName}\"");
                }
            }
        }

        return (T)Delegate.CreateDelegate(typeof(T), targetNode, methodName);
    }
}

/// <summary>
/// Dummy attribute to mark exports that are already handled by an overridden <c>_GetPropertyList</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class ExportFakeAttribute : Attribute
{
    public ExportFakeAttribute()
    { }
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