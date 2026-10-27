# Pause, settings and player audio — 3.41.0

## In game

Escape opens Pause. Choose Resume, Settings or Quit; Quit asks before returning to the desktop. Escape backs out of settings and resumes from the pause page. Closing a machine menu with Escape closes only that menu. The cursor remains captured for gameplay, so resuming needs no extra click. Pausing freezes enemies, waves, machines, projectiles, player movement and the simulation clock. Existing playing machine/SFX voices pause; music continues.

Settings > Sound has independent Master, Music, SFX and UI sliders. Settings > Button Configuration has three pages: movement/interact, other gameplay keys, and mouse fire/aim with sensitivity. Click a key and press its replacement. Escape cancels listening. Duplicate keyboard assignments are rejected; Escape, Enter and arrows remain reserved for navigation. Mouse assignments swap if necessary. Reset Keys restores defaults. Interaction, build and camp prompts reflect rebound keys.

Settings > Graphics switches real Fast/Balanced/High rendering presets and adjusts render resolution from 50% to 100%. It does not alter simulation frequency. Mouse sensitivity is also available there.

Sound, bindings and sensitivity save per project in mining-player-settings.json in the normal game-save directory. Graphics use the native per-title GraphicsSettings3D preferences. Preferences survive sessions. Pause owns an explicit engine Escape handler, without forcing a free cursor during gameplay. Key and mouse aliases feed both direct Event Sheet input and the input-action map; menu input remains physical.

## Editable scene components

Mining company operation > Mining Audio:
- Battle Music: RailJet, volume 0.30, loop with 1.5-second fades. Used during drilling and roaming-spider combat. PYNCHON remains the preparation/camp track.
- Footstep 1–4: alternating clean sand recordings, volume 0.40; walk stride 1.2m, sprint stride 1.6m. Uses actual grounded movement distance, not just held input; silent while idle, in the air, teleported or in menus. Small pitch variation avoids identical repetitions.
- Ammo Pickup Sound: existing pistol-reload.wav. Pitch 1.15, volume 0.50, rate-limited to avoid repeated overlapping sounds when standing at the producing bucket.
- Refinery Volume Multiplier: 4.0. Conveyor volume 0.58; ore impact volume 0.80.

Mining Pause Menu exposes fonts, persistence and sensitivity. Event Sheets include Pause game and open menu, Resume game and close pause menu, and Game is paused. Other audio values remain ordinary editable component properties; Play mining sound cue includes Footstep and AmmoPickup.

## Sources

RailJet — Deceased Superior Technician (DST), CC BY 3.0. Source: https://opengameart.org/content/railjet-long-seamless-loop . Required author link: https://www.nosoapradio.us . Converted OGG to stereo 48 kHz PCM WAV. Attribution is appended to the game's existing audio credits.

Fantozzi sand footsteps — CC0: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone . Four alternate left/right recordings converted to mono 48 kHz PCM WAV. Original archive retained in SourceArt/Pause Audio.

## Main menu concept

This is concept artwork, not a newly implemented startup screen. SourceArt/Main Menu/MainMenu-faded-overlay.png retains title/buttons and the soft black left-side fade; the right side is truly transparent. See Docs/Concepts/AlienMining/MainMenu-brief.md for the image-generation prompt.

No game was exported. Existing standalone builds are unchanged; test the updated project in the editor.
