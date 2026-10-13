from pathlib import Path
p=Path('Plugins/KarlsonDemo/KarlsonDemoPlugin.cs');s=p.read_text();needle='        Condition("canFire"'
a=s.index(needle)
s=s[:a]+'''        Action("tuneShot", "Set damage, fire rate and spread", (w,i,e) => { if(w.SelectedWeapon?.Definition is {} d) { d.DamagePerPellet=Num(i,e,"damage");d.RoundsPerMinute=Num(i,e,"rpm");d.SpreadDegrees=Num(i,e,"spread");d.PelletsPerShot=Math.Clamp((int)Num(i,e,"pellets"),1,32); } },
            Arg("damage","Damage per pellet",VariableValue.FromNumber(25)),Arg("rpm","Rounds per minute",VariableValue.FromNumber(270)),Arg("spread","Spread cone degrees",VariableValue.FromNumber(0)),Arg("pellets","Pellets per shot",VariableValue.FromNumber(1)));
        Condition("fired", "Weapon fired this frame", (w,i,e) => w.SelectedWeapon?.FiredThisFrame==true);
        Condition("hit", "Hitscan hit this frame", (w,i,e) => w.SelectedWeapon?.HitThisFrame==true);
        Condition("reloadComplete", "Reload completed this frame", (w,i,e) => w.SelectedWeapon?.ReloadCompletedThisFrame==true);
''' + s[a:]
s=s.replace('w.GameObject.Variables.Set(EventValueResolver.GetString(i,"currentVariable",e,"Ammo"),','w.GameObject.Variables.Set(Name(i,e,"currentVariable","Ammo"),').replace('w.GameObject.Variables.Set(EventValueResolver.GetString(i,"reserveVariable",e,"Reserve"),','w.GameObject.Variables.Set(Name(i,e,"reserveVariable","Reserve"),')
s=s.replace('    private static FpsGunLab3D? Resolve','    private static string Name(VisualInstruction i, EventExecutionContext e, string key, string fallback)\n    { string value=EventValueResolver.GetString(i,key,e,fallback);return string.IsNullOrWhiteSpace(value)?fallback:value; }\n    private static FpsGunLab3D? Resolve')
p.write_text(s)
