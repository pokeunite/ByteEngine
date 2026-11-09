using ByteEngine.Core.Runtime;
using StbImageSharp;
namespace ByteEngine.Tests;
internal static class ScreenshotTests
{
 public static void Run()
 {
  using var stream=new MemoryStream();
  // OpenGL starts with the bottom row; framebuffer alpha may be zero.
  GameScreenshots.WritePng(stream,new byte[]{255,0,0,0,0,255,0,0,0,0,255,0,255,255,0,0},2,2);
  stream.Position=0;var image=ImageResult.FromStream(stream,ColorComponents.RedGreenBlueAlpha);
  byte[] expected={0,0,255,255,255,255,0,255,255,0,0,255,0,255,0,255};
  if(image.Width!=2||image.Height!=2||!image.Data.SequenceEqual(expected))throw new Exception("Screenshot orientation, opacity or PNG pixels changed");
  if(!GameScreenshots.Request()||GameScreenshots.Request()||!GameScreenshots.BeginCapture())throw new Exception("Screenshot duplicate request guard failed");
  GameScreenshots.Failed(new IOException("test"));
  if(GameScreenshots.IsBusy)throw new Exception("Screenshot did not recover after capture failure");
  Console.WriteLine("PASS screenshot PNG decode, top/bottom orientation, opaque alpha and duplicate/failure recovery");
 }
}
