# Piston elastic shaft repair

The piston (reference ID 18) had almost all vertices rigidly assigned to Moving. Its lower accordion/shaft therefore translated away from its fixed mounting collar on activation.

The lower shaft now blends from Root at its mounting foot to Moving at its head. The upper head remains rigid. Its collider covers the rigid head rather than the entire stretching shaft. No plugin runtime changes or control changes were required. The existing G mechanism input drives its physical extension/retraction.

Installed: Goblin Scraper/Assets/ContraptionParts/goblin_piston.glb and the piston row in both project catalogues. Originals: Goblin Scraper/Backups/piston-animation-repair. Repaired Blender source and renders: Downloads/parts/refined/standard-flight-v3/piston-repaired.

Verified in Blender: lower anchor vertices remain fixed while upper head vertices travel 0.4 m. Extension clip and skin exported. Physics regression: --piston checks extension/retraction and payload socket contact. This fixes elastic continuity; it does not remodel the piston as a hydraulic cylinder.

The main reference generation script includes the repair, and repair_piston_weights.py can regenerate the individual asset.
