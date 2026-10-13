from pathlib import Path
p=Path('Tools/KarlsonDemo/Program.cs');s=p.read_text()
s=s.replace('internal static readonly string[] Names=["Pistol","Shotgun","Rifle"];','internal static readonly string[] Names=["Pistol","Shotgun","Rifle"];')
s=s.replace('C:/Users/codex/Downloads/byte engine files/Karlson_','C:/Users/codex/Downloads/byte engine files/Karlson V2/Karlson_')
s=s.replace('for(int i=0;i<3;i++)events.Rules.Add', 'for(int i=0;i<4;i++)events.Rules.Add')
s=s.replace('DisplayName="Select "+Names[i]', 'DisplayName="Equip slot "+i')
s=s.replace('Actions=[new(){Id="bytebard.karlsondemo.select"+i}]','Actions=[new(){Id="bytebard.karlsondemo.equip",Arguments=new(){["target"]=new(){Constant=VariableValue.FromString("Self")},["slot"]=new(){Constant=VariableValue.FromNumber(i)}}}]')
s=s.replace('for(int i=0;i<3;i++)\n        {\n            string name=Names[i];','for(int i=0;i<4;i++)\n        {\n            string name=i==3?"Pistol":Names[i];')
s=s.replace('name.ToUpperInvariant()+" view model"','(i==3?"PROJECTILE PISTOL":name.ToUpperInvariant())+" view model"')
needle='obj.AddComponent(new AnimationController{AnimationProfile=runtime.Database.ResolveReference("Assets/Animation/"+name+".byteanim")});'
s=s.replace(needle,needle+'''
            obj.AddComponent(new WeaponDefinition3D { DisplayName=i==3?"Heavy projectile pistol":name, Slot=i,
                MagazineCapacity=i==1?6:i==2?24:i==3?8:12, ReserveCapacity=240,
                RoundsPerMinute=i==1?80:i==2?550:i==3?160:270, DamagePerPellet=i==1?12:i==2?18:i==3?65:25,
                PelletsPerShot=i==1?8:1, SpreadDegrees=i==1?6:0, ReloadSeconds=i==1?2:1.8f,
                ReloadMode=i==1?WeaponReloadMode.PerRound:WeaponReloadMode.Magazine,
                ShotMode=i==3?WeaponShotMode.Projectile:WeaponShotMode.Hitscan,
                TriggerMode=i==2?WeaponTriggerMode.Automatic:WeaponTriggerMode.SemiAutomatic,
                ProjectileSpeed=22, ShowShotTrace=true, TraceSeconds=.35f });
            obj.AddComponent(new WeaponInstance3D { CurrentAmmo=i==1?6:i==2?24:i==3?8:12, ReserveAmmo=i==1?30:60 });''')
s=s.replace('player.AddComponent(new HitscanEmitter3D{Range=80,FireInterval=.22f,Damage=25});','')
s=s.replace('player.AddComponent(new FpsGunLab3D{CameraObjectId=eye.Id});','var equipment=player.AddComponent(new FpsGunLab3D{CameraObjectId=eye.Id,InventoryRootId=eye.Id});')
s=s.replace('Text(scene,canvas,"Weapon status","PISTOL 12/12",new(28,657),23);', 'Text(scene,canvas,"Weapon status","PISTOL 12/12",new(28,657),23);equipment.StatusTextObjectId=scene.GameObjects.Single(o=>o.Name=="Weapon status").Id;')
s=s.replace('1 2 3 GUNS','1 2 3 4 GUNS')
s=s.replace('1 pistol, 2 shotgun, 3 rifle;', '1 pistol, 2 shotgun, 3 rifle, 4 heavy projectile pistol;')
s=s.replace('Weapon-only animation clips have no character hands.', 'Animated stylized gloves are included. Select a weapon child: Weapon Definition holds tuning; Weapon Instance holds Current Ammo and Reserve Ammo. The Weapons event category exposes editable actions and conditions.')
# Add individual native weapon Blueprints, with editable definition and instance state.
needle='runtime.Database.Scan();File.WriteAllText(Root+"/Preview/import-validation.json"'
s=s.replace(needle,'''foreach(var gun in eye.Children.Where(o=>o.GetComponent<WeaponDefinition3D>()!=null))
        {
            var data=serialized.GameObjects.Single(o=>o.Id==gun.Id);data.ParentId=null;
            var weaponBlueprint=new ByteEngine.Core.Blueprints.BlueprintDefinition{Name=gun.GetComponent<WeaponDefinition3D>()!.DisplayName,Root=data};
            new ByteEngine.Core.Blueprints.BlueprintSerializer().Save(weaponBlueprint,Root+"/Assets/Blueprints/"+weaponBlueprint.Name+".byteblueprint");
        }
        '''+needle)
p.write_text(s)
