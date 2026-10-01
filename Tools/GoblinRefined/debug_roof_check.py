from pathlib import Path
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text();needle='            Assert(Vector3.Distance(roofBefore,roof.Transform.LocalPosition)';pos=s.index(needle);s=s[:pos]+'''            var roofRig=builder.GameObject.Children.First(o=>o.Name=="Roof mount").GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>()!;
            roofRig.TryGetBoneModelMatrix("Yaw",out var yawMatrix);
            Console.WriteLine($"ROOF_DIAGNOSTIC clip={roofRig.CurrentAnimation} time={roofRig.PlaybackTime} playing={roofRig.IsPlaying} bones={string.Join(',',roofRig.BoneNames)} before={roofRotationBefore} after={roof.Transform.LocalRotation} matrix={yawMatrix} bind={roofRig.ResolvedModel?.Skeleton?.Bones.FirstOrDefault(b=>b.Name=="Yaw")?.BindPose}");
'''+s[pos:];p.write_text(s)
