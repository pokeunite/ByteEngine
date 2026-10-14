using System;
namespace ByteEngine.Core.Animation;
/// <summary>Marks a string property as an action-name selector for the object's animation profile.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AnimationActionPickerAttribute : Attribute { }
