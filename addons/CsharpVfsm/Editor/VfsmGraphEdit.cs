using System;
using System.Collections.Generic;
using System.Linq;

using Godot;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;

[Tool]
public class VfsmGraphEdit : GraphEdit
{
    private VisualStateMachine? MachineNode; 
    private VfsmStateMachine? Machine
        => MachineNode?.Machine;
    
    private CheckBox ProcessToggle = null!;
    private VfsmGraphEditPopup Popup = null!;

    // Used to handle the fact that Godot fires the deselect event after the select event when selecting a node with
    // another node already selected.
    private Node? SelectedNodeTemp = null;

    public override void _Ready()
    {
        PluginTraceEnter();

        Connect("_begin_node_move", this, nameof(On_BeginNodeMove));
        Connect("_end_node_move", this, nameof(On_EndNodeMove));
        Connect("connection_request", this, nameof(On_ConnectionRequest));
        Connect("delete_nodes_request", this, nameof(On_DeleteNodesRequest));
        Connect("gui_input", this, nameof(_UnhandledInput));
        Connect("node_selected", this, nameof(On_NodeSelected));
        Connect("node_unselected", this, nameof(On_NodeDeselected));
        Connect("popup_request", this, nameof(On_PopupRequest));
        
        ProcessToggle = GetNode<CheckBox>("%ProcessToggle");
        ProcessToggle.Connect("toggled", this, nameof(On_ProcessToggle_Toggled));

        Popup = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmGraphEditPopup.tscn"))
            .Instance<VfsmGraphEditPopup>();
        AddChild(Popup);
        
        // Attach to popup
        Popup.Connect("index_pressed", this, nameof(On_Popup_IndexPressed));
        
        PluginTraceExit();
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { ButtonIndex: (int)ButtonList.Left }) {
            Popup.Visible = false; 
        }
    }
    
    public void Edit(VisualStateMachine machine)
    {
        PluginTraceEnter();
        
        if (MachineNode is not null && IsInstanceValid(MachineNode)) {
            MachineNode.Disconnect(nameof(VisualStateMachine.Transitioned), this, nameof(On_MachineNode_Transitioned));
            Machine!.Disconnect("changed", this, nameof(On_Machine_Changed));
        }
        
        // Reset the graph
        ClearConnections();

        // Set up the new machine
        MachineNode = machine;
        MachineNode.Connect(nameof(VisualStateMachine.Transitioned), this, nameof(On_MachineNode_Transitioned));
        Machine!.Connect("changed", this, nameof(On_Machine_Changed));
        Redraw();
        
        MachineNode.EditorProcess = false;
        
        PluginTraceExit();
    }
    
    private void On_Machine_Changed()
    {
        Redraw();
    }
    
    private void On_MachineNode_Transitioned(VfsmState? from, VfsmState to)
    {
        if (from is not null) {
            GetNodeForState(from).Overlay = GraphNode.OverlayEnum.Disabled;
        }
        GetNodeForState(to).Overlay = GraphNode.OverlayEnum.Breakpoint;
    }
    
    private void Redraw()
    {
        if (Machine is null) {
            GD.PushWarning("Attempted to redraw the VfsmGraphEdit but Machine is null");
            return;
        }
        
        var selectedStates = new List<VfsmState>();
        
        PluginTrace("Clearing graph.");

        ClearConnections();

        // Delete all graph nodes
        foreach (var node in StateNodes) {
            if (node.Selected) {
                selectedStates.Add(node.State);
            }
            RemoveChild(node);
            node.QueueFree();
        }
        
        PluginTrace("Drawing graph!");
        
        VfsmStateNodeSpecial? entryNode = null;
        StateNode? entryTransitionTarget = null;
        
        // Create a graph node for each state
        var stateNodeSpecialScene = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmStateNodeSpecial.tscn"));
        var stateNodeScene = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmStateNode.tscn"));
        foreach (var state in Machine.GetStates()) {
            PluginTrace($"Creating state node for {state.Name}");
            // Create a new node based on whether the state is special
            StateNode stateNode;
            if (state is VfsmStateSpecial special) {
                stateNode = stateNodeSpecialScene.Instance<VfsmStateNodeSpecial>()
                        .Init(special);
                
                if (special.SpecialKind is VfsmStateSpecial.Kind.Entry) {
                    entryNode = (VfsmStateNodeSpecial)stateNode;
                }
            } else {
                stateNode = stateNodeScene.Instance<VfsmStateNode>()
                        .Init(state, Machine);
            }

            AddChild(stateNode);

            stateNode.Redraw();

            // Reapply selection
            if (selectedStates.Contains(state)) {
                stateNode.Selected = true;
            }

            if (Machine.EntryTransitionState == state) {
                entryTransitionTarget = stateNode;
            }
        }
        
        Machine.Clean();
        
        PluginTrace("Drawing transitions");
        
        // Draw state transition connections.
        PluginTrace($"{entryNode} {entryTransitionTarget}");
        if (entryNode is not null && entryTransitionTarget is not null) {
            ConnectNode(entryNode.Name, 0, entryTransitionTarget.Name, 0);
        }
        foreach (var state in Machine.GetStates()) {
            foreach (var trigger in state.GetTriggers()) {
                if (Machine.GetTransitions().ContainsKey(trigger)) {
                    var fromNode = GetNodeForState(state);
                    var toNode = GetNodeForState(Machine.GetTransitions()[trigger]);
                    if (state is VfsmStateSpecial) {
                        ConnectNode(fromNode.Name, 0, toNode.Name, 0);
                    } else {
                        var fromIndex = ((VfsmStateNode)fromNode).SlotIndexOfTrigger(trigger);
                        ConnectNode(fromNode.Name, fromIndex, toNode.Name, 0);
                    }
                } 
            }
        }
        
        PluginTrace("Finished drawing graph.");
    }
    
    private StateNode GetNodeForState(VfsmState state)
        => StateNodes.First(n => n.State == state);
    
    private IEnumerable<StateNode> StateNodes
        => this.GetChildNodes().Where(n => n is StateNode).Cast<StateNode>();
    private IEnumerable<StateNode> SelectedNodes
        => StateNodes.Where(n => n.Selected);

    private void On_PopupRequest(Vector2 position)
    {
        Popup.SetPosition(position);
        Popup.Popup_();
    }
    
    private void On_ConnectionRequest(string from, int fromSlot, string to, int toSlot)
    {
        PluginTrace($"Connection request from \"{from}\" to \"{to}\""); 
        var fromNode = GetNode(from);
        var toNode = GetNode(to);
        
        if (fromNode is VfsmStateNodeSpecial special) {
            if (special.SpecialState.SpecialKind is VfsmStateSpecial.Kind.Entry) {
                // Manually assign the entry state
                Machine!.EntryTransitionState = ((StateNode)toNode).State;  
            }
        } else {
            // Create the trigger entry within the machine
            var fromChildren = fromNode.GetChildNodes().Where(n => n.IsInGroup(VfsmStateNode.VfsmConnectionNodeGroup)).ToList();
            if (fromChildren.Count() < fromSlot - 1) {
                GD.PushError($"Attempted to connect from an invalid port (index {fromSlot})");
                return;
            }

            var toTriggerNode = fromChildren[fromSlot];
            if (toTriggerNode is not VfsmStateNodeConnection) {
                GD.PushError($"Attempted to connect from a slot that is not of type {nameof(VfsmStateNodeConnection)}");
                return;
            }

            var trigger = ((VfsmStateNodeConnection)toTriggerNode).Trigger;
        
            Machine!.AddTransition(trigger, ((StateNode)toNode).State);
        }
    }
    
    private void On_BeginNodeMove()
    {
        // Ignore machine changes to avoid weird desync issues due to redrawing while dragging
        Machine!.IgnoreChildChanges = true;
    }
    
    private void On_EndNodeMove()
    {
        Machine!.IgnoreChildChanges = false;
        Redraw();
    }
    
    private void On_NodeSelected(Node node)
    {
        if (node is StateNode stateNode) {
            CsharpVfsmEventBus.Bus.EmitSignal(
                nameof(CsharpVfsmEventBus.ResourceInspectRequested),
                stateNode.State);
        }

        if (SelectedNodes.Any()) {
            SelectedNodeTemp = node;
        }
    }
    
    private void On_NodeDeselected(Node node)
    {
        if (SelectedNodeTemp is null && !SelectedNodes.Except(new []{ node }).Any()) {
            CsharpVfsmEventBus.Bus.EmitSignal(
                nameof(CsharpVfsmEventBus.ResourceInspectRequested),
                Machine);
        }

        SelectedNodeTemp = null;
    }
    
    private void On_DeleteNodesRequest(GodotArray _)
    {
        // The nodes passed in here will exclude any nodes without a "close" button (like ours).
        // We need to manually delete every selected node.
        foreach (var node in StateNodes) {
            if (node.Selected) {
                Machine!.RemoveState(node.State);
            }
        }
    }

    private Vector2 ScreenToGraphOffset(Vector2 screen)
    {
        // TODO Fix this
        var graphPosition = screen * this.Zoom - this.RectGlobalPosition + ScrollOffset;
        PluginTrace($"Screen: {screen} | Graph: {graphPosition}");
        return graphPosition;
    }
    
    private void On_ProcessToggle_Toggled(bool toggled)
    {
        if (toggled) {
            MachineNode!.Start();
            MachineNode!.EditorProcess = true;
        } else {
            MachineNode!.EditorProcess = false;
        }
    }

    private void AddEntryNode()
    {
        var entry = VfsmStateSpecial.Default();
        entry.SpecialKind = VfsmStateSpecial.Kind.Entry;
        AddStateNode(entry);
    }
    
    private void AddExitNode()
    {
        var exit = VfsmStateSpecial.Default();
        exit.SpecialKind = VfsmStateSpecial.Kind.Exit;
        AddStateNode(exit);
    }
    
    private void AddStateNode(VfsmState? state = null)
    {
        if (Machine is null)
            throw new NullReferenceException("Attempted to perform node operation with a null machine");

        if (state is null) {
            state = VfsmState.Default();
        }

        state.Position = ScreenToGraphOffset(Popup.RectGlobalPosition);

        Machine.AddState(state);
    }
    
    private void On_Popup_IndexPressed(int index)
    {
        PluginTrace($"Popup action {index} requested.");
        switch (index) {
            case 0:
                AddEntryNode();
                break;
            case 1:
                AddExitNode();
                break;
            case 2:
                AddStateNode();
                break;
            default:
                throw new ArgumentOutOfRangeException($"Invalid popup index {index}");
        }
    }
}
