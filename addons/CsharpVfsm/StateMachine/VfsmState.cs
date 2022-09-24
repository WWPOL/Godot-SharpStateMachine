using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Godot;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;

[Tool]
public class VfsmState : Resource
{
    [Signal] public delegate void ParentChanged();

    public const string DefaultName = "State";

    /// <summary>
    /// The name of the state. Each state in a machine must have a unique name.
    /// </summary>
    [ExportFake]
    public virtual string Name {
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

    /// <summary>
    /// The name of the function to be called upon a engine <c>Process</c> event. Requires <see cref="TargetPath"/> to
    /// be set in order to take effect.
    /// </summary>
    [ExportFake]
    public string ProcessFunction {
        get => _processFunction;
        set {
            _processFunction = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }
    
    /// <summary>
    /// The offset of this state when displayed on a <see cref="VfsmGraphEdit"/>. It has no effect on the runtime
    /// operations of the state machine.
    /// </summary>
    [ExportFake]
    public Vector2 Position {
        get =>_position;
        set {
            _position = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }
    
    /// <summary>
    /// The <see cref="VfsmTrigger"/>s that this node checks when it is active.
    /// </summary>
    [ExportFake]
    protected List<VfsmTrigger> Triggers = new();
    
    private string _name = "State";
    private string _processFunction = "";
    private Vector2 _position = new(0, 0);
    
    /// <summary>
    /// Create a new state resource. Use this in place of a constructor, if necessary. Required due to Godot custom
    /// node weirdness.
    /// </summary>
    public static VfsmState Default()
        => (VfsmState)GD.Load<VfsmState>(PluginResourcePath("Resources/vfsm_state.tres")).Duplicate();

    public virtual VfsmState Init(VisualStateMachine machineNode)
    {
        PluginTraceEnter();

        Triggers.ForEach(t => t.Init(machineNode));
        SetupDelegates(machineNode, false);
        
        PluginTraceExit();

        return this;
    }

    public void SetupDelegates(VisualStateMachine machineNode, bool recurse = true)
    {
        if (machineNode.TargetNode is not null && !ProcessFunction.Empty()) {
            var node = machineNode.TargetNode;
            if (node is not null) {
                Process = PluginUtil.GetMethodDelegateForNode<Action<float>>(node, ProcessFunction);
            }

            PluginTrace($"Set Process function for state \"{Name}\"");
            // Remember that Process might still be null here.
        }
        
        // TODO OnEnter and OnLeave

        if (recurse) {
            foreach (var trigger in Triggers) {
                trigger.SetupDelegates(machineNode);
            }
        }
    }

    public Action<float>? Process { get; private set; }
    public Action? OnEnter { get; init ;}
    public Action? OnLeave { get; init; }
    
    public virtual void AddTrigger(VfsmTrigger trigger)
    {
        PluginTraceEnter();

        Triggers.Add(trigger);
        EmitChanged();
        PropertyListChangedNotify();
        
        PluginTraceExit();
    }
    
    public virtual bool RemoveTrigger(VfsmTrigger trigger)
    {
        if (Triggers.Remove(trigger)) {
            EmitChanged();
            PropertyListChangedNotify();
            return true;
        }
        
        return false;
    }
    
    public virtual void UpdateTriggers(float delta) {
        foreach (var trigger in GetTriggers()) {
            trigger.Update(delta);
        }
    }
    
    public virtual IList<VfsmTrigger> GetTriggers() => Triggers.AsReadOnly();
    
    private void EmitParentChanged() {
        EmitSignal(nameof(ParentChanged));
    }
    
    public static bool ValidateStateName(string name) {
        return Regex.IsMatch(name, "[_A-Za-z0-9]+");
    }
    
    public override GodotArray _GetPropertyList()
    {
        return new GodotArray(
            PluginUtil.MakeProperty(
                nameof(Name),
                Variant.Type.String
            ),
            PluginUtil.MakeProperty(
                nameof(ProcessFunction),
                Variant.Type.String),
            PluginUtil.MakeProperty(
                nameof(Position),
                Variant.Type.Vector2
            ),
            PluginUtil.MakeProperty(
                nameof(Triggers),
                Variant.Type.Array
            )
        );
    }
}