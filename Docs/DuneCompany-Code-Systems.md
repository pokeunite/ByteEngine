# Dune Company: systems implemented in code

For systems reusable across games, see [Reusable Game Systems](Reusable-Game-Systems.md). Speedometer and minimap now use native engine components; blueprint file storage uses the native save/load service.

This inventory covers game-specific C# logic in Plugins/DuneCompany and Plugins/DesertTerrain, rather than logic authored as event-sheet rows. It excludes general engine features.

Most systems are implemented inside the Dune Vehicle Workshop component (DuneWorkshop3D), split across partial source files. They are not separate components individually. Event-sheet actions can call some of them, but their implementation remains in C#.

| System | What the code handles | Primary source under Plugins/ |
|---|---|---|
| Vehicle construction | Part catalogue, block hierarchy, attachment faces, placement validation, previews, moving, rotating, duplicating and deleting parts | DuneCompany/DuneWorkshop3D.cs; DuneWorkshop3D.Placement.cs; DuneWorkshop3D.Support.cs; BuildStore.cs |
| Build undo/redo | Build history and restoration of construction changes | DuneCompany/DuneWorkshop3D.cs |
| Part tuning and mechanisms | Steering hinges, suspension, servos, pistons and moving mounting outputs | DuneCompany/DuneWorkshop3D.Mechanisms.cs; DuneWorkshop3D.Suspension.cs; DuneWorkshop3D.Ui.cs |
| Vehicle physics and driving | Rigid-body assembly, wheel traction, propulsion, steering, braking, speed limits, terrain collision and recovery | DuneCompany/LandVehiclePhysics.cs; DuneWorkshop3D.Physics.cs; DuneWorkshop3D.Driving.cs |
| Tow recovery | Target vehicle simulation, hitch connection/release, delivery checks and recovery mission state | DuneCompany/LandVehiclePhysics.Recovery.cs; DuneWorkshop3D.Recovery.cs |
| Powered winch | Cable connection, reeling, cable tension, target extraction and winch mission presentation | DuneCompany/LandVehiclePhysics.Winch.cs; DuneWorkshop3D.Winch.cs |
| Contract browser | Available/completed lists, expanded details, required parts and deployment commands | DuneCompany/DuneWorkshop3D.ContractBrowser.cs |
| Campaign saves and rewards | Save-location handling, completion records, balance persistence, payments and campaign reset | DuneCompany/DuneCampaignSaves.cs; DuneWorkshop3D.Recovery.cs; DuneWorkshop3D.Balance.cs |
| Blueprint management | Save new, update loaded build, naming, deletion, loading and saved part/tuning data | DuneCompany/DuneWorkshop3D.Blueprints.cs; BuildStore.cs |
| Blueprint thumbnails | Vehicle preview generation and starter browser previews | DuneCompany/DuneWorkshop3D.BlueprintPreviews.cs |
| Garage UI and onboarding | Garage/build navigation, responsive positioning, guided tutorial steps, required-part highlighting, readiness feedback and settings controls | DuneCompany/DuneWorkshop3D.Garage.cs; DuneWorkshop3D.WorkshopV3.cs |
| Build camera and cutaways | Orbit/zoom, vehicle framing and floor/ceiling visibility while building | DuneCompany/DuneWorkshop3D.Camera.cs; DuneWorkshop3D.Underside.cs |
| Mission HUD | Speedometer, objectives, control hints, wallet and temporary completion presentation | DuneCompany/DuneWorkshop3D.Speedometer.cs; DuneWorkshop3D.HudPolish.cs; DuneWorkshop3D.Balance.cs |
| Vehicle/game feedback | Engine and tyre feedback, dust/exhaust and interaction/reward effects | DuneCompany/DuneWorkshop3D.VehicleFeedback.cs; DuneWorkshop3D.Juice.cs |
| Menu and music audio | Garage music and menu interaction sounds | DuneCompany/DuneWorkshop3D.Music.cs; DuneMenuAudio.cs |
| Main menu | Continue, new-game confirmation, settings, credits and preferences | DuneCompany/DuneMainMenu3D.cs; DuneMainMenu3D.Confirmation.cs |
| Paint | Vehicle part colour/paint handling | DuneCompany/DuneWorkshop3D.Paint.cs |
| Combat prototype | Weapon firing, projectiles and damage to proving-ground targets | DuneCompany/DuneWorkshop3D.Combat.cs; DuneBountyTarget3D.cs |
| Route and minimap | Salvage-route arrival/return state and vehicle position marker | DuneCompany/DuneSalvageRoute3D.cs; DuneMinimap3D.cs |
| World obstacle collision | Scene-authored props participating in vehicle collision | DuneCompany/DuneWorldObstacle3D.cs |
| Interactive desert terrain | Sand sampling, wheel tracks/deformation, local simulation/collision patches, sculpting and roads | DesertTerrain/DesertTerrain3D.cs; DesertTerrain3D.Sculpt.cs; SandSimulationPatch3D.cs; SandCollisionWindow3D.cs; DesertRoad3D.cs; DuneCompany/SandBridge.cs |
| Game diagnostics | Vehicle/workshop diagnostic inspection | DuneCompany/DuneWorkshop3D.Diagnostics.cs |

## Component and event-sheet boundary

DuneCompanyPlugin.cs registers these editor-facing components: Dune Main Menu, Dune Vehicle Workshop, Dune Recovery Contract, Dune Mission Minimap, Dune Salvage Route 3D, Dune World Obstacle 3D, Dune Vehicle Block 3D and Dune Bounty Target 3D.

It also registers event-sheet commands such as Save vehicle, Undo, Open contracts, Deploy contract and Connect recovery. Those are entry points into the C# systems above; they do not recreate those systems in the event sheet.

The combat and salvage systems are prototype capabilities in the source, not a claim that additional finished contracts are available to players.
