using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;

[Tool]
public class VfsmState : Resource
{
    [Signal] public delegate void ParentChanged();

#region Exports

    //[Export]
    private string _name = "State";
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
    
    //[Export]
    private NodePath _targetPath = null!;
    public NodePath TargetPath {
        get => _targetPath;
        set {
            _targetPath = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }

    //[Export]
    private string _processFunction = "";
    public string ProcessFunction {
        get => _processFunction;
        set {
            _processFunction = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }
    
    //[Export]
    private Vector2 _position = new(0, 0);
    public Vector2 Position {
        get =>_position;
        set {
            _position = value;
            EmitChanged();
            EmitParentChanged();
            PropertyListChangedNotify();
        }
    }
    
    //[Export]
    protected List<VfsmTrigger> Triggers = new();

#endregion
    
    public static VfsmState Default()
        => (VfsmState)GD.Load<VfsmState>(PluginResourcePath("Resources/vfsm_state.tres")).Duplicate();

    private Node? Target;

    public virtual VfsmState Init()
    {
        if (!TargetPath.IsEmpty() && !ProcessFunction.Empty() && GetLocalScene() is not null) {
            Target = GetLocalScene().GetNode(TargetPath);
            var methods = Target.GetType().GetMethods();
            
            PluginTrace($"{TargetPath}: {Target.GetType()}");

            if (Target.GetType().GetMethods().Select(m => m.Name).Contains(ProcessFunction)) {
                var parameters = methods.First(m => m.Name == ProcessFunction).GetParameters();
                if (parameters.Count() != 1 || parameters[0].ParameterType != typeof(float)) {
                    GD.PushWarning($"Invalid parameters for process function {ProcessFunction}");
                } else {
                    var method = Target.GetType().GetMethod(ProcessFunction);
                    Process = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), Target, method);
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
        PluginTrace("Emitting to parent");
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
                nameof(TargetPath),
                Variant.Type.NodePath
            ),
            PluginUtil.MakeProperty(
                nameof(ProcessFunction),
                Variant.Type.String
            ),
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
    
    public const string DefaultName = "State";
}