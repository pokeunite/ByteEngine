# All-part connection audit and surface attachments (0.3.2)

Before: 40 of the 65 non-master parts had only their original input connector. Once attached, these parts could not support children. The original suspension moving output passed an isolated placement test, but pointer picking could reject it behind the hovered part's coarse bounds.

The standard catalogue now retains every existing connector and adds accessible axis-face surface connectors on fixed and moving bounds. Moving connectors use the Moving bone so children follow the physical output. Surfaces concealed by another body are omitted; overlapping flanges are deduplicated. Bounds-based surface connectors are a functional attachment approximation, not hand-authored detailed mount geometry. The master remains the original six-face block. Existing machines and authored connector names are retained.

The picker now permits an outward-facing connector on the directly hovered part even if the coarse box lies closer along the ray. Other-object occlusion, occupied sockets, opposing connector normals and spatial clearance remain enforced.

Verification: 65/65 non-master parts accept a short beam; 422/422 outgoing connectors accept the beam at one of four quarter-turns; 4225/4225 isolated non-master parent/child combinations have a valid placement using an input on the child's fixed body. This is an isolated geometric/graph compatibility matrix, not a claim that all crowded assemblies or moving loads are stable. Native Play-loop testing picked the suspension output with the pointer and placed a beam with Enter, verifying ParentBone=Moving; existing build/drive/steering/restore checks also passed.

Installed project package and saved plugin library updated to 0.3.2. Close and reopen the editor to load the new assembly. New surface connector markers appear on existing placed parts after reload. Use Tab to change the selected part's input connector; T selects beam mounting faces. Occupied connectors and solid intersections still reject placement.
