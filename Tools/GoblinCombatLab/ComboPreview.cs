using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
internal sealed class ComboPreview {
 readonly bool pack;readonly SkeletalMeshRenderer[] skins;readonly GameObject[] actors;readonly Scene scene;readonly UiText label;readonly GameObject sword;double elapsed;
 internal ComboPreview(GameProjectRuntime runtime,bool usePack=false){pack=usePack;scene=new("Sword Combo Preview");skins=new SkeletalMeshRenderer[3];actors=new GameObject[3];string[] names=pack?["ComboLight1","ComboLight2","AttackTwoHanded"]:["Attack01_Overhead_InPlace","Attack02_RisingReturn_InPlace","Attack03_DiagonalFinish_InPlace"];
 for(int i=0;i<3;i++){var a=scene.CreateGameObject(names[i]);actors[i]=a;skins[i]=a.AddComponent(new SkeletalMeshRenderer{Model=runtime.Database.ResolveReference(pack?MeleePack.ModelPath:"Assets/Animations/Uthana/Combo/"+names[i]+".glb"),DefaultAnimation=names[i],PlayOnStart=false,Loop=false,Speed=0});}
 sword=pack?Author.ModelPath(scene,runtime,"Preview sword",MeleePack.SwordPath):Author.Model(scene,runtime,"Preview sword","PreviewSword");if(pack){var palette=runtime.Database.ResolveReference(MeleePack.Root+"Textures/HumanAnimations_ColorPalette.png");void Paint(GameObject o){if(o.GetComponent<MeshRenderer>() is {} mesh){mesh.MaterialOverrides.BaseColor=Vector4.One;mesh.MaterialOverrides.SetTexture(MaterialTextureSlot.BaseColor,palette);}foreach(var child in o.Children)Paint(child);}Paint(sword);}var camera=scene.CreateGameObject("Preview camera");camera.AddComponent(new Camera3D{ActiveGameCamera=true,FieldOfView=40,NearClip=.05f,FarClip=50});var position=new Vector3(2.6f,1.8f,-3.7f);camera.Transform.LocalPosition=position;Matrix4x4.Invert(Matrix4x4.CreateLookAt(position,new(0,.85f,0),Vector3.UnitY),out var basis);camera.Transform.LocalRotation=Quaternion.CreateFromRotationMatrix(basis);
 var sun=scene.CreateGameObject("Key light");sun.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(-.6f,.8f,0);sun.AddComponent(new DirectionalLight{Intensity=1.1f,AmbientIntensity=.35f,CastShadows=true,ShadowResolution=1024,ShadowDistance=20});scene.CreateGameObject("Sky").AddComponent(new SkyEnvironment{DrawSky=true,ZenithColor=new(.04f,.07f,.11f),HorizonColor=new(.13f,.2f,.26f),AmbientIntensity=.4f});
 var floor=scene.CreateGameObject("Stage");floor.Transform.LocalScale=new(6,.08f,6);floor.Transform.LocalPosition=new(0,-.045f,0);floor.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){BaseColor=new(.12f,.17f,.20f,1),Roughness=.9f}});
 var hud=scene.CreateGameObject("Labels");hud.AddComponent(new UiCanvas());label=hud.AddComponent(new UiText{Text="COMBO PREVIEW",Offset=new(26,28),FontSize=26,FontReference=runtime.Database.ResolveReference("Assets/Fonts/TypeLightSans.ttf"),ShadowColor=new(0,0,0,.8f)});
 var hint=scene.CreateGameObject("Hint");hint.SetParent(hud,false);hint.AddComponent(new UiText{Text="IN PLACE / HUMAN MELEE PACK / MANNY / LIGHT - LIGHT - HEAVY",Anchor=UiAnchor.BottomLeft,Offset=new(26,-42),FontSize=18,FontReference=runtime.Database.ResolveReference("Assets/Fonts/TypeLightSans.ttf")});
 }
 internal Scene Scene=>scene;
 internal void Step(int frame,bool capture){float time=capture?(frame-1)/30f:(float)(elapsed+=Time.UnscaledDeltaTime);float loop=time%3.15f;float a=pack?.66f:.72f,b=pack?.66f:.62f,c=pack?.80f:.82f;int index=loop<a?0:loop<a+b?1:2;float t=index==0?loop:index==1?loop-a:Math.Min(c,loop-a-b);for(int i=0;i<3;i++){actors[i].Active=i==index;if(i==index){skins[i].ResolveRuntimeResources();skins[i].Play(skins[i].DefaultAnimation,false,0);skins[i].Seek(pack?t*skins[i].Duration/(index==0?a:index==1?b:c):t);}}
 if(skins[index].TryGetBoneWorldMatrix("hand.R",out var hand)){if(pack){Matrix4x4.Decompose(hand,out _,out var handRotation,out var handPosition);sword.Transform.WorldPosition=handPosition+Vector3.Transform(new Vector3(0,.06f,-.02f),handRotation);sword.Transform.WorldRotation=Quaternion.Normalize(handRotation*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2));sword.Transform.WorldScale=Vector3.One;}else{var socket=Matrix4x4.CreateRotationZ(MathF.PI/2)*Matrix4x4.CreateTranslation(0,.085f,-.025f)*hand;Matrix4x4.Decompose(socket,out var scale,out var rotation,out var position);sword.Transform.WorldPosition=position;sword.Transform.WorldRotation=rotation;}} label.Text=pack?$"{index+1:00} / MANNY / {(index==2?"HEAVY FINISHER":"LIGHT STRIKE")}":index==0?"01 / ORIGINAL SWING / IN PLACE":index==1?"02 / RISING RETURN CUT":"03 / DIAGONAL FINISHER";
 }
}




