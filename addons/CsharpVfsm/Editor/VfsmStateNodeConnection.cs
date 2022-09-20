using Godot;
using System;

using static CsharpVfsmPlugin;

[Tool]
public class VfsmStateNodeConnection : PanelContainer
{
    [Signal] public delegate void DeleteRequested();

    private Button DeleteButton = null!;
    private Button InspectButton = null!; 

    public VfsmTrigger Trigger { get; private set; } = null!;
    
    public override void _Ready()
    {
        DeleteButton = GetNode<Button>("%DeleteButton");
        DeleteButton.Connect("pressed", this, nameof(On_DeleteButton_Pressed));

        InspectButton = GetNode<Button>("%InspectButton");
        InspectButton.Connect("pressed", this, nameof(On_InspectButton_Pressed));
        
        Redraw();
    }
    
    public VfsmStateNodeConnection Init(VfsmTrigger trigger)
    {
        Trigger = trigger;
        Trigger.Connect("changed", this, nameof(Redraw));

        return this;
    }
    
    public void Redraw()
    {
        if (Trigger.Kind is VfsmTrigger.TriggerKind.Timer) {
            InspectButton.Text = $"{Trigger.Duration}s";
        }
    }
    
    private void On_DeleteButton_Pressed()
    {
        EmitSignal(nameof(DeleteRequested)); 
    }
    
    private void On_InspectButton_Pressed()
    {
        // Display as a Resource in the inspector
        CsharpVfsmEventBus.Bus.EmitSignal(nameof(CsharpVfsmEventBus.ResourceInspectRequested), Trigger); 
    }
}
