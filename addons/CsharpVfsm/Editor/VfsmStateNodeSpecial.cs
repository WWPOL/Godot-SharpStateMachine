using Godot;

[Tool]
public class VfsmStateNodeSpecial : StateNode
{
    public VfsmStateSpecial SpecialState => (VfsmStateSpecial)State;
    
    public VfsmStateNodeSpecial Init(VfsmStateSpecial state)
    {
        State = state;

        return this; 
    }
    
    public override void Redraw()
    {
        
    }
}
