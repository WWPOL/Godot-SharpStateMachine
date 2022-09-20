using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

using GodotArray = Godot.Collections.Array;

using static CsharpVfsmPlugin;

[Tool]
public class VfsmStateSpecial : VfsmState
{
    //[Export]
    private Kind _specialKind = Kind.Entry;
    public Kind SpecialKind {
        get => _specialKind;
        set {
            _specialKind = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }
    
    public override string Name { get => SpecialKind.ToString(); set {} }

    public new static VfsmStateSpecial Default()
        => (VfsmStateSpecial)GD.Load<VfsmStateSpecial>(PluginResourcePath("Resources/vfsm_state_special.tres")).Duplicate();
    
    private Exception TriggerException => new InvalidOperationException("Special states cannot have triggers");
    public override void AddTrigger(VfsmTrigger _)
        => throw TriggerException;
    public override bool RemoveTrigger(VfsmTrigger _)
        => throw TriggerException;
    public virtual void UpdateTriggers()
        => throw TriggerException;
    
    public override GodotArray _GetPropertyList()
        => new() {
            PluginUtil.MakeProperty(
                nameof(SpecialKind),
                Variant.Type.Int,
                hint: PropertyHint.Enum,
                hintString: PluginUtil.EnumHintString<Kind>()
            ),
                
            // Inherited from parent
            PluginUtil.MakeProperty(
                nameof(Position),
                Variant.Type.Vector2
            )
        };
    
    public enum Kind
    {
        Entry = 1,
        Exit = 2
    } 
}