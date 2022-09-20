using System.Diagnostics;
using System.Runtime.CompilerServices;
using Godot;

[Tool]
public class CsharpVfsmPlugin : EditorPlugin
{
    private VfsmEditor Editor;
    private ToolButton ToolButton;

    public override void _EnterTree()
    {
        AddCustomType(
            nameof(VisualStateMachine),
            nameof(Node),
            GD.Load<Script>(PluginResourcePath("VisualStateMachine.cs")),
            GD.Load<Texture>(PluginResourcePath("Assets/icon.svg")));

        AddCustomType(
            nameof(VfsmStateMachine),
            nameof(Resource),
            GD.Load<Script>(PluginResourcePath("StateMachine/VfsmStateMachine.cs")),
            null
        );

        AddCustomType(
            nameof(VfsmState),
            nameof(Resource),
            GD.Load<Script>(PluginResourcePath("StateMachine/VfsmState.cs")),
            null
        );
        
        AddCustomType(
            nameof(VfsmTrigger),
            nameof(Resource),
            GD.Load<Script>(PluginResourcePath("StateMachine/VfsmTrigger.cs")),
            null
        );

        Editor = GD.Load<PackedScene>(PluginResourcePath("Editor/VfsmEditor.tscn")).Instance<VfsmEditor>();     
        Editor.Theme = GetEditorInterface().GetBaseControl().Theme;
        ToolButton = AddControlToBottomPanel(Editor, "State Machine");
        ToolButton.Hide();

        // Connect to UI event bus.
        CsharpVfsmEventBus.Bus.Connect(nameof(CsharpVfsmEventBus.ResourceInspectRequested), this, nameof(InspectResource));
        
        // Show window if one of our nodes is already selected at load-time.
        var selectedNodes = GetEditorInterface().GetSelection().GetSelectedNodes();
        if (selectedNodes.Count > 0) {
            if (Handles((Object)selectedNodes[0])) {
                Edit((Object)selectedNodes[0]);
                MakeVisible(true);
            }
        }
    }

    public override void _ExitTree()
    {
        RemoveCustomType(nameof(VisualStateMachine));
        RemoveCustomType(nameof(VfsmStateMachine));
        RemoveCustomType(nameof(VfsmState));
        RemoveCustomType(nameof(VfsmTrigger));
        RemoveControlFromBottomPanel(Editor);
        Editor.Free();
    }

    public override bool Handles(Object @object)
        => @object is VisualStateMachine;
    
    public override void MakeVisible(bool visible)
    {
        if (visible) {
            ToolButton.Show();
            MakeBottomPanelItemVisible(Editor);
        } else {
            if (Editor.Visible)
                HideBottomPanel();
            ToolButton.Hide();
        }
        
        Editor.SetProcess(visible);
    }
    
    public override void Edit(Object @object)
    {
        Editor.Edit((VisualStateMachine)@object);
    }

    public void InspectResource(Resource resource)
    {
        PluginTrace($"Inspecting resource {resource}");
        GetEditorInterface().InspectObject(resource, inspectorOnly: true);
    }
    
    public static string PluginResourcePath(string resource)
        => $"res://addons/CsharpVfsm/{resource}";
        
    private static string PathToClassname(string path)
        => System.IO.Path.GetFileName(path).Replace(".cs", "");
    
    [Conditional("VFSM_DEVELOP")]
    public static void PluginTraceEnter(
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "")
    {
        GD.Print($"[{PathToClassname(file)}]={member}() ENTER");
    }

    [Conditional("VFSM_DEVELOP")]
    public static void PluginTraceExit(
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "")
    {
        GD.Print($"[{PathToClassname(file)}]={member}() EXIT");
    }
    
    [Conditional("VFSM_DEVELOP")]
    public static void PluginTrace(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string member = "")
    {
        GD.Print($"[{PathToClassname(file)}] {message}");
    }
}