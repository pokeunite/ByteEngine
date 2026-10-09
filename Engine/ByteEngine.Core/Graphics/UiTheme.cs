using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics;
/// <summary>Inherited button palette. Set a widget ThemeKey to primary, secondary, or danger; blank preserves local authoring.</summary>
public sealed class UiTheme : Component
{
 public ByteEngine.Core.Assets.AssetReference ThemeAsset {get;set;}=ByteEngine.Core.Assets.AssetReference.Empty;
 public void Apply(UiThemePalette palette) {Primary=palette.Primary;Secondary=palette.Secondary;Danger=palette.Danger;Label=palette.Label;PrimaryLabel=palette.PrimaryLabel;Disabled=palette.Disabled;}
 private UiThemePalette? Palette()=>!ThemeAsset.IsEmpty&&ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null?assets.LoadUiTheme(ThemeAsset):null;
 internal Vector4 TextColor(string key){var palette=Palette();return key=="primary"?palette?.PrimaryLabel??PrimaryLabel:palette?.Label??Label;}
 public Vector4 Primary {get;set;}=new(.95f,.65f,.22f,1);public Vector4 Secondary {get;set;}=new(.09f,.1f,.095f,1);public Vector4 Danger {get;set;}=new(.65f,.18f,.12f,1);
 public Vector4 Label {get;set;}=new(.88f,.87f,.83f,1);public Vector4 PrimaryLabel {get;set;}=new(.06f,.06f,.045f,1);public Vector4 Disabled {get;set;}=new(.16f,.17f,.16f,.7f);
 internal static UiTheme? Find(GameObject item){for(GameObject? current=item;current!=null;current=current.Parent)if(current.GetComponent<UiTheme>() is {Enabled:true} theme)return theme;return null;}
 internal Vector4 Background(string key,bool enabled,bool hover,bool down){var palette=Palette();if(!enabled)return palette?.Disabled??Disabled;Vector4 colour=key switch{"primary"=>palette?.Primary??Primary,"danger"=>palette?.Danger??Danger,_=>palette?.Secondary??Secondary};if(hover){float boost=down?.8f:1.12f;colour=new(Math.Min(1,colour.X*boost),Math.Min(1,colour.Y*boost),Math.Min(1,colour.Z*boost),colour.W);}return colour;}
}
