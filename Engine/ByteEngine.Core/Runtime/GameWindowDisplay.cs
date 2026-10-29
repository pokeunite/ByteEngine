namespace ByteEngine.Core.Runtime;
public enum GameWindowMode { Windowed, Borderless, Fullscreen }
/// <summary>Standalone hosts own display changes; editor previews never resize the editor.</summary>
public static class GameWindowDisplay
{
 static Action<GameWindowMode>? apply;
 public static GameWindowMode Mode {get;private set;}=GameWindowMode.Fullscreen;
 public static bool Available=>apply!=null;
 public static void Request(GameWindowMode mode){if(!Enum.IsDefined(mode))throw new ArgumentOutOfRangeException(nameof(mode));Mode=mode;apply?.Invoke(mode);}
 public static IDisposable Attach(Action<GameWindowMode> handler,GameWindowMode initial){var prior=apply;apply=handler;Request(initial);return new Lease(()=>{if(apply==handler)apply=prior;});}
 sealed class Lease(Action release):IDisposable {bool disposed;public void Dispose(){if(disposed)return;disposed=true;release();}}
}
