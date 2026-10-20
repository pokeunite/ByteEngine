namespace ByteEngine.Core.Scene;

/// <summary>Marks a Guid property as an authorable scene-object binding.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SceneObjectReferenceAttribute : Attribute
{
    public Type? RequiredComponent {get;}
    public SceneObjectReferenceAttribute(Type? requiredComponent=null)=>RequiredComponent=requiredComponent;
}
