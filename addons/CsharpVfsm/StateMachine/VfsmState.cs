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

#region Exports

    /// <summary>
    /// The name of the state. Each state in a machine must have a unique name.
    /// </summary>
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
    /// The node which will be referenced when calling state functions such as <see cref="Process"/>.
    /// </summary>
    public NodePath TargetPath {
        get => _targetPath;
        set {
            _targetPath = value;
            EmitChanged();
            PropertyListChangedNotify();
        }
    }

    /// <summary>
    /// The name of the function to be called upon a engine <c>Process</c> event. Requires <see cref="TargetPath"/> to
    /// be set in order to take effect.
    /// </summary>
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
    protected List<VfsmTrigger> Triggers = new();
    
    private string _name = "State";
    private NodePath _targetPath = "";
    private string _processFunction = "";
    private Vector2 _position = new(0, 0);

#endregion
    
    /// <summary>
    /// Create a new state resource. Use this in place of a constructor, if necessary. Required due to Godot custom
    /// node weirdness.
    /// </summary>
    public static VfsmState Default()
        => (VfsmState)GD.Load<VfsmState>(PluginResourcePath("Resources/vfsm_state.tres")).Duplicate();

    private Node? Target;

    public virtual VfsmState Init()
    {
        PluginTraceEnter();

        if (!TargetPath.IsEmpty() && !ProcessFunction.Empty() && GetLocalScene() is not null) {
            Target = GetLocalScene().GetNode(TargetPath);
            var methods = Target.GetType().GetMethods();
            
            PluginTrace($"{TargetPath}: {Target.GetType()}");

            if (Target.GetType().GetMethods().Select(m => m.Name).Contains(ProcessFunction)) {
                var parameters = methods.First(m => m.Name == ProcessFunction).GetParameters();
                if (parameters.Count() != 1 || parameters[0].ParameterType != typeof(float)) {
                    GD.PushWarning($"Invalid parameters for process function {ProcessFunction}");
                } else {
                    var method = Target.GetType().GetMethod(ProcessFunction)!;
                    Process = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), Target, method);
                }
            } else {
                GD.PushWarning($"Process function \"{ProcessFunction}\" not found");
            }
        }
        
        // TODO OnEnter and OnLeave
        
        PluginTraceExit();

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
}