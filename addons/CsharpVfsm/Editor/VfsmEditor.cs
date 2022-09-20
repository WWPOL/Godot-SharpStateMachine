using System.Security.RightsManagement;
using Godot;

using static CsharpVfsmPlugin;

[Tool]
public class VfsmEditor : Control
{
    private VfsmGraphEdit GraphEdit = null!;
    
    public override void _Ready()
    {
        GraphEdit = GetNode<VfsmGraphEdit>("GraphEdit");
    }
    
    public void Edit(VisualStateMachine machine)
    {
        PluginTrace("Editor: Editing machine");
        GraphEdit.Edit(machine);
    }
}