using System.Transactions;
using System;
using System.Linq;
using Godot;
using static CsharpVfsmPlugin;
using GodotArray = Godot.Collections.Array;

/// Manages the runtime operations of a VFSM.
[Tool]
public class VisualStateMachine : Node
{
    [Signal] public delegate void Transitioned(VfsmState? from, VfsmState to);

    [Export]
    public Resource _machine = null!;
    public VfsmStateMachine Machine { 
        get {
            return (VfsmStateMachine)_machine;
        }
        set => _machine = value;
    } 

    private VfsmState? _currentState;
    public VfsmState? CurrentState
    {
        get => _currentState;
        private set {
            if (value is not null && !Machine.GetStates().Contains(value))
                throw new ArgumentException("Current state must be in the FSM's list of states");
            _currentState = value;
        }
    }
    
    public bool EditorProcess = false;
    
    public override void _Ready()
    {
        if (Machine is null || _machine is not VfsmStateMachine) {
            PluginTrace("Initializing new StateMachine");
            Machine = VfsmStateMachine.Default();
        }
        
        Machine.Init(this);
    }

    public override void _Process(float delta)
    {
        if (Engine.EditorHint && !EditorProcess)
            return;

        void ProcessMachineState() {
            if (CurrentState is null)
                return;
                
            if (CurrentState is VfsmStateSpecial special) {
                if (special.SpecialKind is VfsmStateSpecial.Kind.Entry
                    && Machine.EntryTransitionState is not null) {
                    ChangeToState(Machine.EntryTransitionState);
                    return;
                }
            }
            
            if (CurrentState.Process is not null) {
                CurrentState.Process(delta);
            }

            CurrentState.UpdateTriggers(delta);
        }
        
        ProcessMachineState();
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
        
    public void Start()
    {
        if (Machine.EntryTransitionState is null) {
            GD.PushWarning("Unable to start the FSM because no entry transition was found"); 
            return;
        }
        
        Reset();
        ChangeToState(Machine.EntryTransitionState);
    }
    
    public void ChangeToState(VfsmState to, bool force = false)
    {
        PluginTraceEnter();
    
        if (CurrentState is not null && !Machine.StateHasTransition(CurrentState, to) && !force) {
            GD.PushWarning($"Attempted to perform invalid state transition from \"{CurrentState.Name}\" to \"{to.Name}\"");
            return;
        }
        
        PluginTrace("Performing state OnLeave");
        
        // Detach from current state's triggers
        if (CurrentState is not null) {
            foreach (var trigger in CurrentState!.GetTriggers()) {
                trigger.Disconnect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered));
                trigger.Reset();
            }

            if (CurrentState.OnLeave is not null) {
                CurrentState.OnLeave();
            }
        }
        
        PluginTrace($"Transitioning to {to.Name}");
        
        // Do the transition.
        var previousState = CurrentState;
        CurrentState = to; 
        if (CurrentState.OnEnter is not null) {
            CurrentState.OnEnter();
        }
        
        if (CurrentState is not VfsmStateSpecial) {
            PluginTrace("e");
            // Attach to next state's triggers.
            foreach (var trigger in CurrentState!.GetTriggers()) {
                trigger.Connect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered), new GodotArray(trigger));
            }
        }
        
        EmitSignal(nameof(Transitioned), previousState, CurrentState);
        
        PluginTraceExit();
    }
    
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