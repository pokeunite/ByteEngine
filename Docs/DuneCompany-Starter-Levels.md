# Dune Company starter contract levels

Working project: C:/Users/codex/Documents/DuneCompany. Plugin 0.15.0.

## Stranded in the sand
Launch at the depot (0,0). A 70m winding riverbed leads through gentle rises and rock bends to the buggy. The central dune crossing is shorter and less compacted; the western marked track is longer, flatter and firmer. Broad pickup area permits hitch alignment. Bring both vehicles to the green depot bay and stop for payment.

## The Well Ate My Truck
The access road bends west before climbing to a 4m extraction terrace. The buggy sits in a 1.7m shallow pit at (12,70). The south apron is compacted, with room to park and pull over the rounded lip. Connection at the pit and extraction are recorded before delivery can count. Bring the buggy down the access road on the cable and stop both vehicles inside the depot bay. Payment happens once, after delivery, not beside the pit.

## Content and checks
Original 16-bit heightmaps, compaction masks and low-poly sandstone are in Assets/ContractLevels. Terrain rendering and collision use the same heightmap. The depot/garage floor stays at zero. Rocks have static vehicle collision; route posts are visual guides. Existing saves are preserved. No game export was made.

Canonical authoring tool: Tools/DuneCompany/author_starter_levels.py --apply. It creates scene backups under the project's .byteengine/backups. Canonical plugin source remains Plugins/DuneCompany and is synchronized to project PluginSource; packages are synchronized to project Plugins/PluginLibrary and engine PluginLibrary.

Native diagnostic: --dune-starter-levels against an isolated .artifacts project copy. Checks heightfield loading, depot ground, pit depth, route grade, native GPU rendering, no payment at the job site, extraction prerequisite and one stationary delivery payment. Completion-gate tests use controlled reset poses; they are not a full human driving playthrough. First-pass difficulty needs player feedback before scenery/difficulty is expanded. Browser performance has not been measured for these new landscapes.
