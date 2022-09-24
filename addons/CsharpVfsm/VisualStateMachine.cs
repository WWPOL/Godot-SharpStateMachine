using System;

using Godot;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;

/// <summary>
/// A finite state machine. Selecting it in the node tree will show a visual representation of the state machine that
/// can be manipulated as a graph.
///
/// This node manages the runtime state of the state machine. See <see cref="VfsmStateMachine"/> for the resource that
/// stores the static data such as the states and triggers.
/// </summary>
[Tool]
public class VisualStateMachine : Node
{
    /// <summary>
    /// Emitted after the machine has transitioned from state <c>from</c> to state <c>to</c>. If the machine was not
    /// in a state prior to this transition, <c>from</c> will be <c>null</c>.
    /// </summary>
    [Signal] public delegate void Transitioned(VfsmState? from, VfsmState to);

    /// <summary>
    /// The resource containing the state and transition information.
    /// </summary>
    [Export]
    public VfsmStateMachine Machine { get; private set; } = null!;

    /// <summary>
    /// If <c>true</c>, the state machine will automatically start after entering the scene tree.
    /// </summary>
    [Export]
    public bool Autostart = false;
    
    [Export]
    public NodePath TargetPath {
        get => _targetPath;
        set {
            _targetPath = value;
            Machine?.SetupDelegates(this);
        }
    }

    /// <summary>
    /// The current state of the machine, or null if it is not set.
    /// </summary>
    public VfsmState? CurrentState
    {
        get => _currentState;
        private set {
            if (value is not null && !Machine.GetStates().Contains(value))
                throw new ArgumentException("Current state must be in the FSM's list of states");
            _currentState = value;
        }
    }

    public Node? TargetNode => TargetPath.IsEmpty() || !IsInsideTree() ? null : GetNode(TargetPath);

    private NodePath _targetPath = new();
    private VfsmState? _currentState;
    
    public bool EditorProcess = false;

    public override void _Ready()
    {
        if (Machine is null) {
            PluginTrace("Initializing new StateMachine");
            Machine = VfsmStateMachine.Default();
        }
        
        Machine.Init(this);

        if (Autostart) {
            Start();
        }
    }

    public override void _Process(float delta)
    {
        if (Engine.EditorHint && !EditorProcess)
            return;

        if (CurrentState is not null) {
            if (CurrentState is VfsmStateSpecial special) {
                if (special.SpecialKind is VfsmStateSpecial.Kind.Entry && Machine.EntryTransitionState is not null) {
                    // Immediately move to the next state.
                    ChangeToState(Machine.EntryTransitionState);
                    return;
                }
            }

            CurrentState.Process?.Invoke(delta);

            CurrentState.UpdateTriggers(delta);
        }
    }

    private void On_StateTransitionTriggered(VfsmTrigger trigger)
    {
        PluginTraceEnter();

        trigger.Reset();
        if (Machine.GetTransitions().ContainsKey(trigger)) {
            ChangeToState(Machine.GetTransitions()[trigger]);
        }
        
        PluginTraceExit();
    }
    
    /// <summary>
    /// Resets the machine and begins operation from the entry node. Will print a warning and return if there is no
    /// entry node. The machine will be started at the entry node; therefore, a signal will be fired immediately
    /// after calling this to indicate that a state transition has occurred to the start state.
    ///
    /// For starting the machine at a specified node, see <see cref="ChangeToState"/>.
    /// </summary>
    public void Start()
    {
        if (!Machine.HasSpecialState(VfsmStateSpecial.Kind.Entry)) {
            GD.PushWarning("Unable to start the FSM because no entry transition was found"); 
            return;
        }
        
        Reset();
        ChangeToState(Machine.GetSpecialState(VfsmStateSpecial.Kind.Entry)!);
    }
    
    /// <summary>
    /// Perform a transition to the state <c>to</c>. If the current state of the machine lacks a direct connection to the
    /// target state, <c>force</c> must be set to true. Otherwise, a warning will be printed and the function will exit.
    /// </summary>
    public void ChangeToState(VfsmState to, bool force = false)
    {
        PluginTraceEnter();
    
        if (CurrentState is not null && !Machine.StateHasTransition(CurrentState, to) && !force) {
            GD.PushWarning($"Attempted to perform invalid state transition from \"{CurrentState.Name}\" to \"{to.Name}\"");
            return;
        }
        
        // Detach from current state's triggers
        if (CurrentState is not null) {
            foreach (var trigger in CurrentState!.GetTriggers()) {
                trigger.Disconnect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered));
                trigger.Reset();
            }

            CurrentState.OnLeave?.Invoke();
        }
        
        // Do the transition.
        var previousState = CurrentState;
        CurrentState = to; 
        CurrentState.OnEnter?.Invoke();
        
        if (CurrentState is not VfsmStateSpecial) {
            // Attach to next state's triggers.
            foreach (var trigger in CurrentState!.GetTriggers()) {
                trigger.Connect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered), new GodotArray(trigger));
            }
        }
        
        EmitSignal(nameof(Transitioned), previousState, CurrentState);
        
        PluginTraceExit();
    }
    
    /// <summary>
    /// Resets the machine. The machine will have no assigned state after the reset, so it will not perform any
    /// processing until <see cref="Start"/> or <see cref="ChangeToState"/> is called.
    /// </summary>
    public void Reset()
    {
        foreach (var t in Machine.GetTransitions().Keys) {
            t.Reset();
        }
        
        if (CurrentState is not null) {
            foreach (var trigger in CurrentState.GetTriggers()) {
                trigger.Disconnect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered));
            }
        }
        
        CurrentState = null;
    }
}