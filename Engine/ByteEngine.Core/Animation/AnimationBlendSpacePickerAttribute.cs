namespace ByteEngine.Core.Animation;

/// <summary>Profile blendspace selector. Resolves a controller binding on this object or its ancestors.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AnimationBlendSpacePickerAttribute : Attribute
{
    public string ControllerObjectProperty { get; }
    public AnimationBlendSpacePickerAttribute(string controllerObjectProperty = "AnimationControllerObjectId")
        => ControllerObjectProperty = controllerObjectProperty;
}
