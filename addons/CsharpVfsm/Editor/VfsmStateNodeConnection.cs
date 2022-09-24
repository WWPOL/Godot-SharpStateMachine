using System;
using System.Threading;

using Godot;

using static CsharpVfsmPlugin;

[Tool]
public class VfsmStateNodeConnection : PanelContainer
{
    [Signal] public delegate void DeleteRequested();

    private static readonly Lazy<StreamTexture> TimerIcon = new(
        () => GD.Load<StreamTexture>(PluginResourcePath("Assets/timer.svg")));

    private static readonly Lazy<StreamTexture> FunctionIcon = new(
        () => GD.Load<StreamTexture>(PluginResourcePath("Assets/check_function.svg")));

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
        InspectButton.Text = Trigger.Kind switch {
            VfsmTrigger.TriggerKind.Timer => $"{Trigger.Duration}s",
            VfsmTrigger.TriggerKind.Condition => Trigger.CheckFunction,
            _ => null
        };
        
        InspectButton.Icon = Trigger.Kind switch
        {
            VfsmTrigger.TriggerKind.Timer => TimerIcon.Value,
            VfsmTrigger.TriggerKind.Condition => FunctionIcon.Value,
            _ => null
        };
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
