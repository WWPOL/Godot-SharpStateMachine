using Godot;

[Tool]
public class StateNode : GraphNode
{
    public virtual VfsmState State { get; set; }

    public override void _Ready()
    {
        Connect("offset_changed", this, nameof(On_OffsetChanged));
    }
    
    public virtual void Redraw() { }

    private void On_OffsetChanged()
    {
        State.Position = Offset;
    }
}