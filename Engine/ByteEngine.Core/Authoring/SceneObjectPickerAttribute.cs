using System;

namespace ByteEngine.Core.Authoring;

/// <summary>
/// Marks a string property as a scene-object name reference. The inspector draws
/// a searchable object dropdown instead of a raw text field, while the stored
/// value stays the object's name (readable in scene JSON and scriptable).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SceneObjectPickerAttribute : Attribute
{
}
