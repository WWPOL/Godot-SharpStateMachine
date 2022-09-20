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
    public VfsmStateMachine? Machine
        => MachineNode?.Machine;
    
    private CheckBox ProcessToggle = null!;
    private VfsmGraphEditPopup Popup = null!;

    public override void _Ready()
    {
        PluginTraceEnter();

        Connect("_begin_node_move", this, nameof(On_BeginNodeMove));
        Connect("_end_node_move", this, nameof(On_EndNodeMove));
        Connect("connection_request", this, nameof(On_ConnectionRequest));
        Connect("delete_nodes_request", this, nameof(On_DeleteNodesRequest));
        Connect("gui_input", this, nameof(_UnhandledInput));
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
        if (ev is InputEventMouseButton mouseEvent) {
            if (mouseEvent.ButtonIndex == (int)ButtonList.Left) {
                Popup.Visible = false; 
            }
        }
    }
    
    public void Edit(VisualStateMachine machine)
    {
        PluginTraceEnter();
        
        if (MachineNode is not null) {
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
    
    private void On_MachineNode_Transitioned(VfsmState from, VfsmState to)
    {
        GetNodeForState(from).Overlay = GraphNode.OverlayEnum.Disabled;
        GetNodeForState(to).Overlay = GraphNode.OverlayEnum.Breakpoint;
    }
    
    private IEnumerable<VfsmStateNode> SelectedNodes
        => this.GetChildNodes().Where(f => f is VfsmStateNode node && node.Selected).Cast<VfsmStateNode>();
    
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
        foreach (var node in GetChildren().Cast<Node>().Where(t => t is VfsmStateNode)) {
            if (((VfsmStateNode)node).Selected) {
                selectedStates.Add(((VfsmStateNode)node).State);
            }
            RemoveChild(node);
            node.QueueFree();
        }
        
        PluginTrace("Drawing graph!");
        
        // Create a graph node for each state
        var stateNodeScene = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmStateNode.tscn"));
        foreach (var state in Machine.GetStates()) {
            var stateNode = stateNodeScene.Instance<VfsmStateNode>()
                .Init(state, Machine);
            AddChild(stateNode);
            stateNode.Redraw();

            // Reapply selection
            if (selectedStates.Contains(state)) {
                stateNode.Selected = true;
            }
        }
        
        Machine.Clean();
        
        // Draw state transition connections.
        foreach (var state in Machine.GetStates()) {
            foreach (var trigger in state.GetTriggers()) {
                if (Machine.GetTransitions().ContainsKey(trigger)) {
                    var fromNode = GetNodeForState(state);
                    var toNode = GetNodeForState(Machine.GetTransitions()[trigger]);
                    var fromIndex = fromNode.SlotIndexOfTrigger(trigger);
                    ConnectNode(fromNode.Name, fromIndex, toNode.Name, 0);
                } 
            }
        }
    }
    
    private VfsmStateNode GetNodeForState(VfsmState state)
    {
        var child = GetChildren().Cast<Node>()
            .Where(n => n is VfsmStateNode).Cast<VfsmStateNode>()
            .FirstOrDefault(n => n.State == state);
        if (child is null)
            throw new ArgumentException($"No node found for state \"{state}\"");
        return child;
    }

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
        
        Machine!.AddTransition(((VfsmStateNodeConnection)toTriggerNode).Trigger, ((VfsmStateNode)toNode).State);
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
    
    private void On_DeleteNodesRequest(GodotArray _)
    {
        // The nodes passed in here will exclude any nodes without a "close" button (like ours).
        // We need to manually delete every selected node.
        foreach (var node in this.GetChildNodes().Where(n => n is VfsmStateNode).Cast<VfsmStateNode>()) {
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
            if (SelectedNodes.Count() == 0) {
                ProcessToggle.Pressed = false;
                return;
            }
            
            MachineNode!.ChangeToState(SelectedNodes.First().State);
            MachineNode!.EditorProcess = true;
        } else {
            MachineNode!.EditorProcess = false;
        }
    }

    private void AddEntryNode()
    {
    }
    
    private void AddExitNode()
    {
    }
    
    private void AddStateNode()
    {
        if (Machine is null)
            throw new NullReferenceException("Attempted to perform node operation with a null machine");

        var state = VfsmState.Default();
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
