using Godot;
using System;

public class TestScene : Control
{
    private VisualStateMachine Machine = null!;
    private RichTextLabel StateLabel = null!;
    private RichTextLabel InstructionLabel = null!;

    public override void _Ready()
    {
        Machine = GetNode<VisualStateMachine>("VisualStateMachine");
        StateLabel = GetNode<RichTextLabel>("%StateLabel");
        InstructionLabel = GetNode<RichTextLabel>("%InstructionLabel");

        Machine.Connect(nameof(VisualStateMachine.Transitioned), this, nameof(On_Machine_Transitioned));
        
        StateLabel.BbcodeText = string.Empty;
        InstructionLabel.BbcodeText = string.Empty;
    }

    private void On_Machine_Transitioned(VfsmState? from, VfsmState to)
    {
        StateLabel.BbcodeText = StateLabel_Format(to.Name);
        InstructionLabel.BbcodeText = to.Name switch {
            "State3" => InstructionLabel_Format("Press Space to continue"),
            _ => string.Empty
        };
    }
    
    private void State_State1_Process(float delta)
    {
        GD.Print("Hello from State1!");
    }
    
    private void State_State2_Process(float delta)
    {
        GD.Print("Hello from State2!");
    }

    private bool State_State3_CheckAdvance()
    {
        return Input.IsKeyPressed((int)KeyList.Space);
    }
    
    private string StateLabel_Format(string stateName)
        => $"[center]Current state: [b]{stateName}[/b][/center]";

    private string InstructionLabel_Format(string text)
        => $"[center]{text}[/center]";
}
