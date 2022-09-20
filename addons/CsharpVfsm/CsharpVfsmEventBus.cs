using Godot;

public class CsharpVfsmEventBus : Object
{ 
    [Signal] public delegate void ResourceInspectRequested(Resource resource);
    
    public static CsharpVfsmEventBus Bus;
    
    static CsharpVfsmEventBus()
    {
        Bus = new();
    }
}