using System;
using System.Linq;

using Godot;

using static CsharpVfsmPlugin;

using GodotArray = Godot.Collections.Array;

[Tool]
public class VfsmStateNode : StateNode
{
    public const string VfsmConnectionNodeGroup = "__vfsm_connection";

    private static StyleBoxFlat StyleBoxSelected = null!;
    
    private Button NewTriggerButton = null!;

    private VfsmStateMachine Machine = null!;
    
    public VfsmStateNode()
    {
        if (StyleBoxSelected is null) {
            var frame = GetStylebox("frame");
            if (frame is StyleBoxFlat) {
                StyleBoxSelected = (StyleBoxFlat)frame.Duplicate();
                StyleBoxSelected.SetBorderWidthAll(1);
                StyleBoxSelected.BorderColor = Colors.White;
                AddStyleboxOverride("selectedframe", StyleBoxSelected);
            }
        }
    }
    
    public VfsmStateNode Init(VfsmState state, VfsmStateMachine machine)
    {
        State = state;
        Machine = machine;

        return this; 
    }
    
    public override void _Ready()
    {
        base._Ready();

        NewTriggerButton = GetNode<Button>("NewTriggerButton");

        NewTriggerButton.Connect("pressed", this, nameof(On_NewTriggerButton_Pressed));
        
        State.Connect("changed", this, nameof(Redraw));
    }

    /// Populates the contents of this control.
    public override void Redraw()
    {
        Title = State.Name;
        
        // Clear slot nodes
        foreach (var child in this.GetChildNodes().Where(n => n.IsInGroup(VfsmConnectionNodeGroup))) {
            DetachConnectionNode((VfsmStateNodeConnection)child);
            RemoveChild(child);
            child.QueueFree();
        }
    
        // Add new connection nodes for our triggers
        var connectionScene = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmStateNodeConnection.tscn"));
        Node? belowNode = null;
        foreach (var trigger in State.GetTriggers()) {
            var connectionNode = connectionScene.Instance<VfsmStateNodeConnection>()
                .Init(trigger);

            AttachConnectionNode(connectionNode);

            if (belowNode is null) {
                AddChild(connectionNode);
            } else {
                AddChildBelowNode(belowNode, connectionNode);
            }

            belowNode = connectionNode;
        }
        
        if (belowNode is not null) {
            // Move the "add" button to the bottom
            RemoveChild(NewTriggerButton);
            AddChildBelowNode(belowNode, NewTriggerButton);
        }

        // Set slot data.
        foreach (var (node, i) in this.GetChildNodes().Select((n, i) => (n, i))) {
            if (node.IsInGroup(VfsmConnectionNodeGroup)) {
                SetSlotEnabledRight(i, true);
            }
        }
    }
    
    public int SlotIndexOfTrigger(VfsmTrigger trigger)
    {
        var triggerNodes = this.GetChildNodes()
            .Where(n => n.IsInGroup(VfsmConnectionNodeGroup) && n is VfsmStateNodeConnection)
            .Cast<VfsmStateNodeConnection>()
            .ToList();

        var index = triggerNodes.FindIndex(n => n.Trigger == trigger);
        if (index < 0) {
            throw new ArgumentException($"Cannot find a trigger node for trigger {trigger} in state node {this}");
        }
                
        return index;
    }
    
    private void AttachConnectionNode(VfsmStateNodeConnection connectionNode)
    {
        connectionNode.AddToGroup(VfsmConnectionNodeGroup);
        connectionNode.Connect(nameof(VfsmStateNodeConnection.DeleteRequested), this, nameof(On_ConnectionNode_DeleteRequested), new GodotArray(connectionNode));
    }

    private void DetachConnectionNode(VfsmStateNodeConnection connectionNode)
    {
        connectionNode.Disconnect(nameof(VfsmStateNodeConnection.DeleteRequested), this, nameof(On_ConnectionNode_DeleteRequested));
    }

    private void On_ConnectionNode_DeleteRequested(VfsmStateNodeConnection node)
    {
        State.RemoveTrigger(node.Trigger);
        Machine.RemoveTransition(node.Trigger);
    }
    
    private void On_NameEdit_TextEntered(string newText)
    {
        State.Name = newText;
    }
    
    private void On_NewTriggerButton_Pressed()
    {
        PluginTraceEnter();

        var trigger = VfsmTrigger.Default();
        State.AddTrigger(trigger);
        
        PluginTraceExit();
    }
}
