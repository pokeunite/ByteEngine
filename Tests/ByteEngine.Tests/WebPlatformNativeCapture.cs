using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using OpenTK.Graphics.OpenGL4;
namespace ByteEngine.Tests;
internal sealed class WebPlatformNativeCapture(string phase):ByteEngineApplication(1280,720,"Web platform native comparison")
{
 GameProjectRuntime? project;int frames;
 protected override bool ShouldUpdateScene=>false;
 protected override void OnEngineStart(){IsVisible=false;project=new(WebPlatformFixture.ProjectFile,Console.WriteLine);Scenes.LoadScene(project.LoadStartupScene());for(int i=0;i<120;i++){Time.Update(1.0/60);Scenes.UpdateInternal();}if(phase=="flow"){Scenes.ActiveScene!.Variables["Fill"].Boolean=true;for(int i=0;i<183;i++){Time.Update(1.0/60);Scenes.UpdateInternal();}}}
 protected override void OnEngineRender(){if(++frames<3)return;GL.Finish();int w=ClientSize.X,h=ClientSize.Y;byte[] pixels=new byte[w*h*4];GL.ReadPixels(0,0,w,h,PixelFormat.Bgra,PixelType.UnsignedByte,pixels);using var image=new System.Drawing.Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var data=image.LockBits(new(0,0,w,h),System.Drawing.Imaging.ImageLockMode.WriteOnly,image.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(pixels,0,data.Scan0,pixels.Length);image.UnlockBits(data);image.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);Directory.CreateDirectory(Path.Combine(WebPlatformFixture.Root,"Reviews"));string file=Path.Combine(WebPlatformFixture.Root,"Reviews","Native-"+phase+".png");image.Save(file);Console.WriteLine("Native comparison "+file);if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Native comparison GPU error");Close();}
}
