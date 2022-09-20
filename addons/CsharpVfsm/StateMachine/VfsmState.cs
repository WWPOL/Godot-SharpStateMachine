using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

using static CsharpVfsmPlugin;
using System.Text.RegularExpressions;

[Tool]
public class VfsmState : Resource
{
    [Signal] public delegate void ParentChanged();

#region Exports

    private string _name = "State";
    [Export]
    public string Name {
        get => _name;
        set {
            if (ValidateStateName(value)) {
                _name = value;
                EmitChanged();
                EmitParentChanged();
                PropertyListChangedNotify();
            }
        }
    }
    
    private NodePath _targetPath = null!;
    [Export]
    public NodePath TargetPath {
        get => _targetPath;
        set {
            _targetPath = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }

    private string _processFunction = null!;
    [Export]
    public string ProcessFunction {
        get => _processFunction;
        set {
            _processFunction = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }
    
    private Vector2 _position = new(0, 0);
    [Export]
    public Vector2 Position {
        get =>_position;
        set {
            _position = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }
    
    [Export]
    private List<VfsmTrigger> Triggers = new();

#endregion
    
    public static VfsmState Default()
        => (VfsmState)GD.Load<VfsmState>(PluginResourcePath("Resources/vfsm_state.tres")).Duplicate();

    private Node? Target;

    public VfsmState Init(Node parent)
    {
        Target = parent.GetNode(TargetPath);
        var methods = Target.GetType().GetMethods();

        if (ProcessFunction is not null) {
            if (methods.Select(m => m.Name).Contains(ProcessFunction)) {
                var parameters = methods.First(m => m.Name == ProcessFunction).GetParameters();
                if (parameters.Count() != 1 || parameters[0].ParameterType != typeof(float)) {
                    GD.PushWarning($"Invalid parameters for process function {ProcessFunction}");
                } else {
                    Process = (Action<float>)Target.GetType().GetMethod(ProcessFunction).CreateDelegate(typeof(Action<float>));
                }
            } else {
                GD.PushWarning($"Process function \"{ProcessFunction}\" not found");
            }
        }
        
        // TODO OnEnter and OnLeave
        
        return this;
    }

    public Action<float>? Process { get; private set; }
    public Action? OnEnter { get; init ;}
    public Action? OnLeave { get; init; }
    
    public void AddTrigger(VfsmTrigger trigger)
    {
        PluginTraceEnter();

        Triggers.Add(trigger);
        EmitChanged();
        PropertyListChangedNotify();
        
        PluginTraceExit();
    }
    
    public bool RemoveTrigger(VfsmTrigger trigger)
    {
        if (Triggers.Remove(trigger)) {
            EmitChanged();
            PropertyListChangedNotify();
            return true;
        }
        
        return false;
    }
    
    public IList<VfsmTrigger> GetTriggers() => Triggers.AsReadOnly();
    
    private void EmitParentChanged() {
        PluginTrace("Emitting to parent");
        EmitSignal(nameof(ParentChanged));
    }
    
    public static bool ValidateStateName(string name) {
        return Regex.IsMatch(name, "[_A-Za-z0-9]+");
    }
    
    public const string DefaultName = "State";
}