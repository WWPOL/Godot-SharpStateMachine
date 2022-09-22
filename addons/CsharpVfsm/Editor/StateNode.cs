using Godot;

[Tool]
public abstract class StateNode : GraphNode
{
    public VfsmState State { get; protected set; } = null!;

    public override void _Ready()
    {
        Offset = State.Position;
        Connect("offset_changed", this, nameof(On_OffsetChanged));
    }
    
    private void On_OffsetChanged()
    {
        State.Position = Offset;
    }

    public abstract void Redraw();
}