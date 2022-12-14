using System;

using Godot;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;
using GodotDictionary = Godot.Collections.Dictionary;

[Tool]
public class VfsmTrigger : Resource
{
    /// To be fired when a change is made on this object that requires its parent to update.
    [Signal] public delegate void ParentChanged();

    [Signal] public delegate void Triggered();
    
    public override GodotArray _GetPropertyList()
        => new() {
            PluginUtil.MakeProperty(
                nameof(Kind),
                Variant.Type.Int,
                PropertyUsageFlags.Default,
                PropertyHint.Enum,
                PluginUtil.EnumHintString<TriggerKind>()),
            PluginUtil.MakeProperty(
                nameof(Duration),
                Variant.Type.Real,
                Kind is TriggerKind.Timer 
                    ? PropertyUsageFlags.Default 
                    : PropertyUsageFlags.Storage),
            PluginUtil.MakeProperty(
                nameof(CheckFunction),
                Variant.Type.String,
                Kind is TriggerKind.Condition
                    ? PropertyUsageFlags.Default
                    : PropertyUsageFlags.Storage)
        };
    
    /// <summary>
    /// The method of activation for this trigger.
    /// </summary>
    [ExportFake]
    public TriggerKind Kind {
        get => (TriggerKind)_kind;
        set {
            _kind = (int)value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        } 
    }

    /// <summary>
    /// If <see cref="Kind"/> is <see cref="TriggerKind.Timer"/>, the duration after which this will be triggered.
    /// </summary>
    [ExportFake]
    public float Duration {
        get => _duration;
        set {
            _duration = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }

    public string CheckFunction {
        get => _checkFunction;
        set {
            _checkFunction = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }

    private int _kind = (int)TriggerKind.Timer;
    private float _duration = 0.5f;
    private string _checkFunction = string.Empty;

    private float TimerTime = 0f;
    private Func<bool>? CheckFunctionDelegate = null!;

    /// <summary>
    /// Create a new trigger resource. Use this in place of a constructor, if necessary. Required due to Godot
    /// custom node weirdness.
    /// </summary>
    public static VfsmTrigger Default()
        => (VfsmTrigger)GD.Load<Resource>(PluginResourcePath("Resources/vfsm_trigger.tres")).Duplicate();
    
    private VfsmTrigger()
    { }

    public VfsmTrigger Init(VisualStateMachine machineNode)
    {
        if (Kind is TriggerKind.Condition) {
            SetupDelegates(machineNode);
        }

        return this;
    }

    public void Update(float delta)
    {
        if (Kind is TriggerKind.Timer) {
            TimerTime += delta;
            if (TimerTime >= Duration)
                EmitSignal(nameof(Triggered));
        } else if (Kind is TriggerKind.Condition) {
            if (CheckFunctionDelegate is not null) {
                if (CheckFunctionDelegate()) {
                    EmitSignal(nameof(Triggered));
                }
            }
        }
    }

    public void SetupDelegates(VisualStateMachine machineNode, bool recurse = true)
    {
        if (CheckFunction.Empty() || machineNode.TargetNode is null) {
            CheckFunctionDelegate = null;
        } else {
            CheckFunctionDelegate = PluginUtil.GetMethodDelegateForNode<Func<bool>>(machineNode.TargetNode, CheckFunction);
        }
    }

    /// <summary>
    /// Resets the internal state of this node to its defaults.
    /// </summary>
    public void Reset()
    {
        TimerTime = 0f;
    }

    private void EmitParentChanged()
        => EmitSignal(nameof(ParentChanged));

    public enum TriggerKind
    {
        Timer = 1,
        Condition = 2
    }
}