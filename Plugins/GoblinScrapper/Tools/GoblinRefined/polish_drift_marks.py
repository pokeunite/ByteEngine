from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('            mark.Object.Active=true; _skidLife[i]=6;', '            mark.Object.Transform.LocalScale=new(.13f,.006f,Math.Max(.24f,Velocity.Length()*.085f));\n            mark.Object.Active=true; _skidLife[i]=6;');p.write_text(s)
for name in ['Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs','Tests/ByteEngine.Tests/Program.cs']:
 p=Path(name);p.write_text(p.read_text().rstrip()+'\n')
