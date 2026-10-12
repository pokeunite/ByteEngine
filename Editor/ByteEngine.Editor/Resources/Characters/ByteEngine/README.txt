ByteEngine mannequin animation bundle

Mannequin.glb: full body.
Mannequin-FPS-Arms.glb: camera-relative arms.
Mannequin-Advanced-FPS.glb: arms and legs, no visible torso/head/pelvis. Hidden torso bones retain locomotion. Place at player feet, not at camera height.

Each model contains 18 AF_ clips: 15 ActionForge clips and 3 pistol-holding locomotion variants.
Use AF_pistol_idle_loop, AF_pistol_walk_loop, AF_pistol_jog_loop for armed locomotion.
AF_pistol_shoot and AF_pistol_reload are character animations; weapon-part animations must be synchronized separately.
Root translation stays in place while vertical body motion is preserved. Original hand.R bone is retained for sockets.
