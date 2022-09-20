using Godot;
using System;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using static CsharpVfsmPlugin;

[Tool]
public class VfsmStateNode : GraphNode
{
    public const string VfsmConnectionNodeGroup = "__vfsm_connection";
    
    private LineEdit NameEdit = null!;
    private Button NewTriggerButton = null!;

    private VfsmStateMachine Machine = null!;
    public VfsmState State { get; private set; } = null!;
    
    public VfsmStateNode Init(VfsmState state, VfsmStateMachine machine)
    {
        State = state;
        Machine = machine;

        Offset = State.Position;

        return this; 
    }
    
    public override void _Ready()
    {
        Connect("offset_changed", this, nameof(On_OffsetChanged));

        NameEdit = GetNode<LineEdit>("NameEdit");
        NewTriggerButton = GetNode<Button>("NewTriggerButton");

        NameEdit.Connect("text_entered", this, nameof(On_NameEdit_TextEntered));
        NewTriggerButton.Connect("pressed", this, nameof(On_NewTriggerButton_Pressed));
        
        State.Connect("changed", this, nameof(Redraw));
    }

    /// Populates the contents of this control.
    public void Redraw()
    {
        // Update name
        NameEdit.Text = State.Name; 

        // Clear slot nodes
        foreach (var child in this.GetChildNodes().Where(n => n.IsInGroup(VfsmConnectionNodeGroup))) {
            RemoveChild(child);
            child.QueueFree();
        }
    
        // Add new connection nodes for our triggers
        var connectionScene = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmStateNodeConnection.tscn"));
        foreach (var trigger in State.GetTriggers()) {
            var connectionNode = connectionScene.Instance<VfsmStateNodeConnection>()
                .Init(trigger);
            connectionNode.AddToGroup(VfsmConnectionNodeGroup);
            AddChild(connectionNode);
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
    
    private void On_OffsetChanged()
    {
        PluginTrace($"Dragged to {Offset}"); 
        State.Position = Offset;
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
