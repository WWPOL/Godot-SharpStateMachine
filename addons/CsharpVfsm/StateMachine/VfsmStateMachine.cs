using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Collections.Immutable;

using static CsharpVfsmPlugin;
using Microsoft.SqlServer.Server;
using System.Net.NetworkInformation;

/// The static data of a VFSM state machine, including states and transitions.
[Tool]
public class VfsmStateMachine : Resource
{
    [Export]
    private readonly List<VfsmState> States = new();
    
    [Export]
    private readonly Dictionary<VfsmTrigger, VfsmState> Transitions = new();
    
    public static VfsmStateMachine Default()
        => (VfsmStateMachine)GD.Load<VfsmStateMachine>(PluginResourcePath("Resources/vfsm_state_machine.tres")).Duplicate();
    
    public bool IgnoreChildChanges = false;
    
    public void Init()
    {
        PluginTrace($"Initializing with {States.Count} states");
        States.ForEach(c => AttachChild(c, nameof(VfsmState.ParentChanged)));
        Transitions.Keys.ToList().ForEach(c => AttachChild(c, nameof(VfsmTrigger.ParentChanged)));
    }
    
    public void AddState(VfsmState state)
    {
        States.Add(state);
        AttachChild(state, nameof(VfsmState.ParentChanged));
        EmitChanged();
        PropertyListChangedNotify();
    }
    
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
    
    public void AddTransition(VfsmTrigger trigger, VfsmState to)
    {
        if (ValidateTransition(trigger, to)) {
            Transitions[trigger] = to;
            AttachChild(trigger, nameof(VfsmTrigger.ParentChanged));
            EmitChanged();
            PropertyListChangedNotify();
        }
    }
    
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

    public void RemoveRelatedTransitions(VfsmState state)
    {
        foreach (var t in Transitions.ToList()) {
            if (state.GetTriggers().Contains(t.Key) || t.Value == state) {
                Transitions.Remove(t.Key);
            }
        }
    }
    
    public VfsmState? GetTriggerOwner(VfsmTrigger trigger)
        => States.Where(s => s.GetTriggers().Contains(trigger)).FirstOrDefault();
    
    public IList<VfsmState> GetStates() => States.Cast<VfsmState>().ToList().AsReadOnly();
    public IDictionary<VfsmTrigger, VfsmState> GetTransitions() => Transitions.ToImmutableDictionary();
    
    /// Attempts to rectify any invalid data in the machine, such as triggers with deleted targets.
    public void Clean()
    {
        var removals = new List<VfsmTrigger>();
        foreach (var trigger in Transitions.Keys) {
            if (!ValidateTransition(trigger, Transitions[trigger]))  {
                removals.Add(trigger);
            }
        }
        removals.ForEach(t => RemoveTransition(t));
        
        // Ensure all names are valid and not duplicate
        var names = new List<string>();
        foreach (var state in GetStates()) {
            if (!VfsmState.ValidateStateName(state.Name)) {
                state.Name = VfsmState.DefaultName;
            }
            
            // Add an incrementing number to duplicate names.
            var suffix = "2";
            while (names.Contains(state.Name)) {
                state.Name += suffix;
            }
            
            names.Add(state.Name);
        }
    }

    /// Ensure a trigger connection is valid in this state machine.
    /// It is assumed that the presence of an invalid transition is a program error, so a warning will be printed
    ///  for any violation found.
    private bool ValidateTransition(VfsmTrigger trigger, VfsmState state)
    {
        // Make sure the state is in the machine.
        if (!States.Contains(state))  {
            GD.PushWarning($"Transition is invalid because target state {state} is not in machine's states");
            return false;
        }
        
        var triggerOwner = States.Where(s => s.GetTriggers().Contains(trigger)).FirstOrDefault();
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
        foreach (var s in GetStates()) {
            foreach (var t in s.GetTriggers()) {
                if (Transitions.ContainsKey(t) && Transitions[t] == state)
                    return t;
            }
        }
        return null;
    }
    
    private void AttachChild(Godot.Object child, string signal)
    {
        child.Connect(signal, this, nameof(ChildChanged));
    }
    
    private void DetachChild(Godot.Object child, string signal)
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