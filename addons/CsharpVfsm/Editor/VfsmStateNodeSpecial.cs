using Godot;

using static CsharpVfsmPlugin;

[Tool]
public class VfsmStateNodeSpecial : StateNode
{
    public override VfsmState State { get; set; } = null!;
    private VfsmStateMachine Machine = null!;

    public VfsmStateSpecial SpecialState => (VfsmStateSpecial)State;
    
    public VfsmStateNodeSpecial Init(VfsmStateSpecial state, VfsmStateMachine machine)
    {
        State = state;
        Machine = machine;
        
        Offset = state.Position;

        return this; 
    }
    
    public override void _Ready()
    {
        base._Ready();
    }
    
    public override void Redraw()
    {
        
    }

    private void LoadLazyAssets()
    {
        PluginTraceEnter();

        PluginTraceExit();
    }
}
