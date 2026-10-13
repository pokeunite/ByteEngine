from pathlib import Path
p=Path('Tools/KarlsonDemo/CyberpunkImport.cs');s=p.read_text().replace('new(.18f,-.20f,-.49f):new(.20f,-.22f,-.68f)','new(.14f,-.015f,-.50f):new(.18f,-.045f,-.72f)').replace('Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);','Quaternion.CreateFromYawPitchRoll(MathF.PI+.08f,name=="Pistol"?-.085f:-.14f,0);');p.write_text(s)
