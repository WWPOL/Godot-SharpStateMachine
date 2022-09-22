using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Godot;

using GodotArray = Godot.Collections.Array;

using static CsharpVfsmPlugin;
using static PluginUtil;

/// <summary>
/// The static data of a state machine, including states and transitions.
/// </summary>
[Tool]
public class VfsmStateMachine : Resource
{
    public override GodotArray _GetPropertyList()
        => new() {
            MakeProperty(
                nameof(States),
                Variant.Type.Array),
            MakeProperty(
                nameof(Transitions),
                Variant.Type.Dictionary),
            // Store the entry transition as a list...
            MakeProperty(
                nameof(EntryTransitionStates),
                Variant.Type.Array,
                usage: PropertyUsageFlags.Storage),
            // ...but display it as a state
            MakeProperty(
                nameof(EntryTransitionState),
                Variant.Type.Object,
                usage: PropertyUsageFlags.Editor)
        };
    
    /// <summary>
    /// The possible states of the machine. The machine must contain a state that it is trying to switch to.
    /// </summary>
    [ExportFake]
    private readonly List<VfsmState> States = new();

    /// <summary>
    /// The transitions the machine will follow when the given trigger (represented as the key) is triggered.
    /// </summary>
    [ExportFake]
    private readonly Dictionary<VfsmTrigger, VfsmState> Transitions = new();

    /// <summary>
    /// If we attempt to store a plain resource reference in an export field, it will result in that node being
    /// duplicated on an object level when instancing the scene. This causes equality checks that normally should be
    /// passing to fail, since to the engine, the value of the field is considered to be different from any of the
    /// values stored in <see cref="States"/>. To counteract this, we store the reference as a single-element array,
    /// which retains the reference when constructing the scene.
    /// </summary>
    [ExportFake]
    private List<VfsmState?> EntryTransitionStates = new(1);

    /// <summary>
    /// The state that will be activated when the machine is started. Visually, it is represented as the state connected
    /// to the "entry" node of the machine.
    ///
    /// Note that this is internally represented as a list, but that detail should be ignored when using this field.
    /// See <see cref="EntryTransitionStates"/> for more information.
    /// </summary>
    public VfsmState? EntryTransitionState {
        get => EntryTransitionStates.FirstOrDefault();
        set {
            if (EntryTransitionStates.Any())
                EntryTransitionStates[0] = value;
            else
                EntryTransitionStates.Add(value);
            EmitChanged();
            PropertyListChangedNotify();
        }
    }

    /// <summary>
    /// Used by the editor to temporarily disable triggering 
    /// </summary>
    public bool IgnoreChildChanges = false;
    
    /// <summary>
    /// Create a new state machine resource. Use this in place of a constructor, if necessary. Required due to Godot
    /// custom node weirdness.
    /// </summary>
    public static VfsmStateMachine Default()
        => (VfsmStateMachine)GD.Load<VfsmStateMachine>(PluginResourcePath("Resources/vfsm_state_machine.tres")).Duplicate();
    
    private VfsmStateMachine()
    { }
    
    /// <summary>
    /// Perform post-load resource initialization. This must be called at some point before using the state machine if
    /// this resource is being loaded from disk.
    /// </summary>
    public virtual void Init(VisualStateMachine machineNode)
    {
        PluginTraceEnter();

        PluginTrace($"Initializing with {States.Count} states");
        foreach (var state in States) {
            PluginTrace($"Adding state {state.Name}");
            state.Init();
            AttachChild(state, nameof(VfsmState.ParentChanged));
        }
        Transitions.Keys.ToList().ForEach(c => AttachChild(c, nameof(VfsmTrigger.ParentChanged)));
        
        PluginTraceExit();
    }
    
    /// <summary>
    /// Attempt to add a state to the machine.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the state was successfully added. <c>false</c> if the addition failed, such as when trying to
    /// add a duplicate special state.
    /// </returns>
    public bool AddState(VfsmState state)
    {
        PluginTraceEnter();

        // Ensure we don't add a duplicate special state
        if (state is VfsmStateSpecial special && HasSpecialState(special.SpecialKind)) {
            GD.PushWarning($"Cannot add another state of special kind {special.SpecialKind}");
            return false;
        }

        States.Add(state);
        AttachChild(state, nameof(VfsmState.ParentChanged));
        EmitChanged();
        PropertyListChangedNotify();

        Clean();
        
        PluginTraceExit();

        return true;
    }
    
    /// <summary>
    /// Attempt to remove a state from the machine.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a state was removed, otherwise <c>false</c>.
    /// </returns>
    public bool RemoveState(VfsmState state)
    {
        if (States.Remove(state)) {
            // Remove all related transitions
            RemoveRelatedTransitions(state);

            // Remove from machine
            DetachChild(state, nameof(VfsmState.ParentChanged));
            EmitChanged();
            PropertyListChangedNotify();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Attempt to add a transition to the machine.
    /// </summary>
    /// <returns><c>true</c> if the transition was successfully added, otherwise <c>false</c>.</returns>
    public bool AddTransition(VfsmTrigger trigger, VfsmState to)
    {
        if (ValidateTransition(trigger, to)) {
            Transitions[trigger] = to;
            AttachChild(trigger, nameof(VfsmTrigger.ParentChanged));
            EmitChanged();
            PropertyListChangedNotify();
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Attempt to remove a transition from the machine.
    /// </summary>
    /// <returns><c>true</c> if a transition was successfully removed, otherwise <c>false</c>.</returns>
    public bool RemoveTransition(VfsmTrigger trigger)
    {
        if (Transitions.Remove(trigger)) {
            DetachChild(trigger, nameof(VfsmTrigger.ParentChanged));
            EmitChanged();
            PropertyListChangedNotify();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Remove all transitions related to a state (that is, all transitions that lead to that state or come from a
    /// trigger owned by that state).
    /// </summary>
    public void RemoveRelatedTransitions(VfsmState state)
    {
        foreach (var t in Transitions.ToList()) {
            if (state.GetTriggers().Contains(t.Key) || t.Value == state) {
                Transitions.Remove(t.Key);
            }
        }
    }

    /// <summary>
    /// Get the state owner of a trigger within this machine.
    /// </summary>
    /// <returns>
    /// The state that owns the given trigger, or <c>null</c> if it or the trigger does not exist within the machine.
    /// </returns>
    public VfsmState? GetTriggerOwner(VfsmTrigger trigger)
        => States.FirstOrDefault(s => s.GetTriggers().Contains(trigger));
    
    /// <summary>
    /// Get the states contained in this machine.
    /// </summary>
    public IList<VfsmState> GetStates()
        => States.AsReadOnly();
    
    /// <summary>
    /// Get the transitions contained in this machine.
    /// </summary>
    public IDictionary<VfsmTrigger, VfsmState> GetTransitions()
        => Transitions.ToImmutableDictionary();
    
    /// <summary>
    /// Get a reference to the special state of the given kind contained within the machine, or <c>null</c> if it does
    /// not exist.
    /// </summary>
    public VfsmStateSpecial? GetSpecialState(VfsmStateSpecial.Kind kind) 
        => (VfsmStateSpecial?)GetStates().FirstOrDefault(s => s is VfsmStateSpecial special 
                                                              && special.SpecialKind == kind);
    
    /// <summary>
    /// Check if the machine has a special state of the given kind.
    /// </summary>
    public bool HasSpecialState(VfsmStateSpecial.Kind kind) => GetSpecialState(kind) is not null;

    /// <summary>
    /// Check if a direct transition between a trigger of state <c>from</c> to state <c>to</c> exists.
    /// </summary>
    public bool StateHasTransition(VfsmState from, VfsmState to)
        => (from is VfsmStateSpecial special && special.SpecialKind is VfsmStateSpecial.Kind.Entry && EntryTransitionState == to)
            ||  GetTransitions().Any(kv => from.GetTriggers().Contains(kv.Key) && kv.Value == to);
    
    /// <summary>
    /// Attempts to rectify any invalid data in the machine, such as triggers with deleted targets.
    /// </summary>
    public void Clean()
    {
        var removals = Transitions.Keys.Where(trigger => !ValidateTransition(trigger, Transitions[trigger]));
        removals.ToList().ForEach(t => RemoveTransition(t));
        
        // Ensure all names are valid and not duplicate
        var names = new List<string>();
        foreach (var state in GetStates()) {
            if (!VfsmState.ValidateStateName(state.Name)) {
                state.Name = VfsmState.DefaultName;
            }
            
            // Add an incrementing number to duplicate names.
            const string suffix = "2";
            while (names.Contains(state.Name)) {
                state.Name += suffix;
            }
            
            names.Add(state.Name);
        }
    }

    /// <summary>
    /// Ensure a trigger connection is valid in this state machine.
    /// 
    /// It is assumed that the presence of an invalid transition is a program error, so a warning will be printed
    /// for any violation found.
    /// </summary>
    private bool ValidateTransition(VfsmTrigger trigger, VfsmState state)
    {
        // Make sure the state is in the machine.
        if (!States.Contains(state))  {
            GD.PushWarning($"Transition is invalid because target state {state} is not in machine's states");
            return false;
        }
        
        var triggerOwner = States.FirstOrDefault(s => s.GetTriggers().Contains(trigger));
        if (triggerOwner is null || triggerOwner == state) {
            GD.PushWarning($"Transition is invalid because trigger owner is invalid");
            return false;
        }
        
        var inbound = StateInboundConnection(state);
        if (inbound != trigger && inbound is not null) {
            GD.PushWarning("Transition is invalid because target state already has an inbound connection");
            return false;
        }
        
        return true;
    }
    
    private VfsmTrigger? StateInboundConnection(VfsmState state)
    {
        return GetStates()
            .SelectMany(s => s.GetTriggers())
            .FirstOrDefault(t => Transitions.ContainsKey(t) && Transitions[t] == state);
    }
    
    private void AttachChild(Object child, string signal)
    {
        if (!child.IsConnected(signal, this, nameof(ChildChanged))) {
            child.Connect(signal, this, nameof(ChildChanged));
        }
    }
    
    private void DetachChild(Object child, string signal)
    {
        child.Disconnect(signal, this, nameof(ChildChanged));
    }
    
    private void ChildChanged()
    {
        if (IgnoreChildChanges)
            return;
        PluginTrace($"Child changed!");
        EmitChanged();
    }
}
