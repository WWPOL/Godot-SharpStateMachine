using System;
using System.Linq;
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
    
    //[Export]
    private int _kind = (int)TriggerKind.Timer;
    public TriggerKind Kind {
        get => (TriggerKind)_kind;
        set {
            _kind = (int)value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        } 
    }

    //[Export]
    private float _duration = 0.5f;
    public float Duration {
        get => _duration;
        set {
            _duration = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }

    private float TimerTime = 0f;
    
    public static VfsmTrigger Default()
        => (VfsmTrigger)GD.Load<Resource>(PluginResourcePath("Resources/vfsm_trigger.tres")).Duplicate();
    
    public void Update(float delta)
    {
        if (Kind is TriggerKind.Timer) {
            TimerTime += delta;
            if (TimerTime >= Duration)
                EmitSignal(nameof(Triggered));
        }
    }
    
    public void Reset()
    {
        TimerTime = 0f;
    }

    public override GodotArray _GetPropertyList()
    {
        var kindEnumHintString = Enum.GetValues(typeof(TriggerKind)).Cast<TriggerKind>().Aggregate("", (str, next) => $"{str}{next}:{(int)next},");
        kindEnumHintString = kindEnumHintString.Remove(kindEnumHintString.Length - 1, 1);

        var props = new GodotArray {
            MakeProperty(
                nameof(Kind),
                Variant.Type.Int,
                PropertyUsageFlags.Default,
                PropertyHint.Enum,
                kindEnumHintString),
            MakeProperty(
                nameof(Duration),
                Variant.Type.Real,
                Kind is TriggerKind.Timer ? PropertyUsageFlags.Default : PropertyUsageFlags.Storage)
        };

        return props;
    }

    private static GodotDictionary MakeProperty(
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
    
    private void EmitParentChanged()
        => EmitSignal(nameof(ParentChanged));

    public enum TriggerKind
    {
        Timer = 1,
        Condition = 2
    }
}