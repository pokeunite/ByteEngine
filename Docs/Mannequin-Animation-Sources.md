# Free mannequin gun animation sources

Research date: 9 October 2026. Scope: locate sources for later retargeting to the full-body mannequin and FPS arms, then redistribution in ByteEngine starter projects. This is a source catalogue, not a downloaded clip inventory. No animations have been retargeted or installed by this research step.

Implementation update: Quaternius Standard, PSX FPS Arms Free and Drillimpact archives were downloaded and retargeted. Their bundled GLBs contain 131 full-body clips (including root-motion variants) and 88 FPS clips. Exact inventories and notices are in `Editor/ByteEngine.Editor/Resources/Characters/ByteEngine/animations.json`. Rokoko's public archive actually contains 15 FBX clips, rather than the page's advertised 11; these are processed separately pending redistribution rights. The eight shortlisted Sketchfab packs require sign-in; the user's external browser is not exposed to the current tools. RGSDev's current pack is **paid ($1.30)**, so it was excluded from the free download pass.

Bundle update, 9 October 2026: the user requested the combined All-Available models in all starter projects. The standard engine GLBs now contain 146 full-body / 103 FPS clips and include the fifteen retargeted Rokoko captures as mannequin example-project assets under the author's stated personal/commercial 3D project-use permission. This is not a finding that Rokoko offers a general standalone-animation redistribution license. A separate `ROKOKO-SOURCE-NOTICE.txt` records the grant and terms; those contributions are not represented as CC0/CC BY. The earlier separation notes below describe the original research assessment.

## Full body

| Source | Free content / format | Redistribution assessment | Next inspection |
| --- | --- | --- | --- |
| [Quaternius Universal Animation Library](https://quaternius.itch.io/universal-animation-library) | Free **Standard** archive; humanoid library with movement and gun actions. Author supplies engine exports and root-motion / disabled-root-motion versions. | **CC0**, suitable for bundled defaults. | Inspect Standard archive for exact gun clips and formats. The advertised 120+ total is for the library as a whole; Pro and Blender Source downloads are paid tiers. Do not count all 120+ as free. |
| [Rokoko: 11 free gun animations](https://www.rokoko.com/resources/rokoko-mocap-11-free-gun-animations) | 11 full-body gun-handling mocap recordings including fingers; FBX, Mixamo skeleton, 30 FPS. | Author permits personal and commercial projects, but the page does not clearly grant redistribution as editable engine-template assets. **Pending; exclude from bundled defaults until confirmed.** | Requires contact form and marketing consent. Exact clip names not listed on page. |

Quaternius is the first full-body candidate to inspect. A complete freely redistributable full-body rifle/shotgun combat set has **not** been established by this search. FPS arm clips alone do not provide full-body locomotion. Any missing full-body gun movements will require another source or authored animation.

## FPS arms: recommended download candidates

Each Cransh page below lists a free download and a Creative Commons Attribution license. Retain the source-page attribution and confirm the specific license version in the downloaded package. Counts and action names are not published clearly enough to treat these as an exact clip inventory yet. A weapon name identifies a pack, not a guarantee of every reload/aim/movement variant.

| Creator / pack | Weapon coverage | Source |
| --- | --- | --- |
| Cransh — Animated FPS hands, rifle animation pack | Rifle / ACR | [Download page](https://sketchfab.com/3d-models/animated-fps-hands-rifle-animation-pack-5f2d0ed780a94724b36ab505f7564057) |
| Cransh — FPS pistol animations | Pistol | [Download page](https://sketchfab.com/3d-models/fps-pistol-animations-0d7a343dcb6f401197a73c91aee93f6d) |
| Cransh — FPS AK-74M animations | Assault rifle | [Download page](https://sketchfab.com/3d-models/fps-ak-74m-animations-94be8385c402474cacd39bc096c6ca14) |
| Cransh — Lowpoly MP5 animations | SMG | [Download page](https://sketchfab.com/3d-models/fps-animations-lowpoly-mp5-568f00dd76944baaa5eae1a1cc871423) |
| Cransh — Sniper rifle animations | Sniper rifle | [Download page](https://sketchfab.com/3d-models/fps-animations-sniper-rifle-c15ae8393d824f5b929e3f69691cdd31) |
| Cransh — Remington shotgun | Shotgun | [Download page](https://sketchfab.com/3d-models/fps-arms-remington-shotgun-e68ef617fe8a48cca8610d016ffd5881) |
| Cransh — Saiga animations remake | Magazine-fed shotgun | [Download page](https://sketchfab.com/3d-models/fps-arms-saiga-animations-remake-74e30b71e4a049b2a82b9d18f58e623c) |
| BarcodeGames — M4 FPS weapon animations pack v1 | Rifle: draw, single fire, reload, empty reload, holster — **5 listed clips** | [Download page](https://sketchfab.com/3d-models/m4-fps-weapon-animations-pack-v1-662fc74dda2646cfb48fc610705768ef) |
| heyheythere — PSX FPS Arms Free | Pistol: idle, draw, holster, fire, reload, inspect — **6 gun clips**. Also 10 bare-hand, 5 knife and 6 flashlight clips; **27 total** | [Download page](https://heyheythere.itch.io/psx-fps-arms-free) |

BarcodeGames lists Creative Commons Attribution and free download. Its walk, run, melee, jump and pickup additions are planned, not included in the five listed clips.

PSX FPS Arms Free explicitly uses **CC BY 4.0** and supplies GLB animation clips, FBX takes and a Blender file, at 30 FPS. Its paid 210-animation pack is separate. The free source uses three bones per finger and a right-hand weapon attachment bone. Its required credit line is:

`PSX FPS Arms Free by heyheythere - https://heyheythere.itch.io/psx-fps-arms-free - CC BY 4.0`

Cransh packs credit additional hand and weapon model creators. Preserve all relevant package credits when retaining their content. Use original author downloads rather than mirrors with conflicting licenses. The Remington page has a NoAI tag; keep it out of generative-model inputs. Blender rig mapping and deterministic animation baking are the intended workflow.

## Supplemental sources and candidates with unresolved issues

| Source | Findings | Status |
| --- | --- | --- |
| [Drillimpact PSX First Person Arms](https://drillimpact.itch.io/psx-first-person-arms-free) | CC0; FBX, GLB and Blend; 17 clips including relax, punches, pushes, grabs, guard, knife and finger-gun gestures. | Useful hand/melee supplement. Finger-gun gestures are not firearm reload/shooting animation sets. |
| [RGSDev Low Poly FPS Starter Kit](https://rgsdev.itch.io/low-poly-fps-starter-kit) | M416, AWM, Glock G48, knife and grenade; author lists idle, fire, ADS, ADS fire, reload, draw, holster, walk, run, jump, melee and throw. Author comments allow broad use. | No clear formal redistribution license established here; users report truncated FBX animations while Unity package works. Inspect license and actual clips before selecting. |
| [Quaternius Animated Guns](https://quaternius.com/packs/animatedguns.html) | Six animated gun models; CC0; FBX, OBJ and Blend. | Weapon mechanism animations only. Useful to coordinate slides/bolts/magazines later; not full-body or arms animations. |

## Excluded from automatic starter bundling

- **Mixamo:** free to use in finished games, but Adobe's licensing FAQ excludes redistributing its files as engine templates/asset packs. Retargeting does not provide a separate redistribution grant. [Adobe Mixamo licensing FAQ](https://community.adobe.com/questions-696/mixamo-faq-licensing-royalties-ownership-eula-and-tos-589400?lang=en).
- **MoCap Online free samples:** sample pricing does not remove licensing restrictions. Its legal page excludes standalone animation redistribution and raw animation files in template projects. [Author legal policy](https://mocaponline.com/pages/legal).
- **Paid packs and paid tiers:** excluded from this free source list. Do not count planned clips, animated-but-nondownloadable previews, or static rigged arms as free animation packs.

## Requirements for the later retargeting pass

1. Download original free archives and retain the included license, author attribution, source URL and archive hash.
2. Inventory actual animation names, durations, bone hierarchies, frame rates, root motion and weapon channels. Mark missing coverage instead of assuming it exists.
3. Map deform bones and fingers to our mannequin rigs; align rest poses, scale and axes; bake to the exported deform skeletons rather than IK helper bones.
4. Check weapon grip, support-hand contacts and magazine/bolt handling. Arms animation retargeting cannot by itself supply a matching animated weapon.
5. Validate full-body foot contacts and FPS framing in playback before selecting defaults. Export reusable clips with stable names and ship attribution/license notices in every starter project.

Target coverage to check: unarmed and armed idle; aim; directional movement and sprint; standing/crouched variants; fire; ADS fire; normal/empty reload; draw; holster; weapon switch; recoil; melee; inspect; grenade actions; hit and death for the full body. This is a coverage checklist, not a claim that all these actions were found.

[CC0 terms](https://creativecommons.org/publicdomain/zero/1.0/) permit modification and redistribution without attribution. [CC BY 4.0 terms](https://creativecommons.org/licenses/by/4.0/) permit sharing and adaptation, including commercially, with credit, a license link and indication of changes; preserve those rights for downstream starter-project users.
