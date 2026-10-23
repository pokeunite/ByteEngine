namespace ByteEngine.Core.Gameplay;

/// <summary>Optional component contribution to the native character diagnostic HUD.</summary>
public interface ICharacterDebugSource
{
    string GetCharacterDebugText();
}
