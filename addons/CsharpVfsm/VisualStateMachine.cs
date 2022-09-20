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
        
        Machine.Init();
    }

    public override void _Process(float delta)
    {
        if (Engine.EditorHint && !EditorProcess)
            return;

        void ProcessMachineState() {
            if (CurrentState is null)
                return;
            
            if (CurrentState.Process is not null) {
                CurrentState.Process(delta);
            }

            foreach (var trigger in CurrentState.GetTriggers()) {
                trigger.Update(delta);
            }
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
    
    public void ChangeToState(VfsmState to)
    {
        PluginTraceEnter();

        // Detach from current state's triggers
        if (CurrentState is not null) {
            foreach (var trigger in CurrentState!.GetTriggers()) {
                trigger.Disconnect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered));
            }

            // Perform the OnLeave
            if (CurrentState.OnLeave is not null) {
                CurrentState.OnLeave();
            }
        }
        
        // Do the transition.
        var previousState = CurrentState;
        CurrentState = to; 
        if (CurrentState.OnEnter is not null) {
            CurrentState.OnEnter();
        }

        // Attach to next state's triggers.
        foreach (var trigger in CurrentState!.GetTriggers()) {
            trigger.Connect(nameof(VfsmTrigger.Triggered), this, nameof(On_StateTransitionTriggered), new GodotArray(trigger));
        }
        
        EmitSignal(nameof(Transitioned), previousState, CurrentState);
        
        PluginTraceExit();
    }
}