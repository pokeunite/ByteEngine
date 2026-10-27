# Mining interface — in-game setup

The approved reference is implemented as separate live screens: compact combat HUD, contextual machine panel and commodity market. The world models and combat balance are unchanged by this pass.

## Playing

- Walk near the drill, ammo press, refinery or terminal and press **E**. The closest eligible machine owns the prompt.
- Walk up to the **company tent** and press **E** to access the computer. Remote Tab access is removed.
- **Escape**, the top-right X, or Close dismisses a panel. Movement is restored to its previous state. Mouse clicks on panels do not fire the gun.
- Start an operation at the drill. The permanent shop sidebar and previous HUD are disabled.
- At the ammo press, Output, Storage and Efficiency are independent purchases. Collect transfers available production into the equipped gun's reserve, limited by that gun's reserve capacity. RESERVE FULL explains an unavailable collection.
- At the refinery, upgrade **batch size**, **processing time** and **recovery yield** separately. Input and promised output are locked when a batch begins. Reserved ore cannot be sold until processing finishes.
- At the company terminal, open the linked **permanent upgrade map** or the market. The map has weapon, industry and survival branches, prerequisites, exact effects and saved ownership.
- In the market, choose raw/refined ore, drag the quantity slider, then sell the displayed number of units. The sell total rounds down to whole credits, as shown on the button.

The fictional market updates continuously, including while the market screen is open between waves. Its default interval is one second. Its current quote, bounded price history, random state and update clock are saved; reopening the panel does not reroll prices. New companies start with one current quote, and history builds as the game runs. Both resource prices use the same market multiplier applied to their different base prices. This is not a connection to a real stock exchange.

## Editing without changing source

In Main.bytescene:

- **Mining company operation → Mining Operation**: extraction, waves, base resource prices, drill/refinery/company upgrade effects and persistence.
- **Mining company operation → Mining Workshop and Market**: ammo output/storage/efficiency effects and costs; maximum upgrade level; market volatility, bounds and update interval; separate workshop persistence.
- **Mining business interface → Mining Business Interface**: scene references, fonts, machine/ore illustrations, colours and interaction radius. The presentation has a shared reference layout, while button objects provide native pointer/keyboard interaction.
- **Assets/Events/Mining/Mining business controls.byteevents**: every key and button binding, opening/closing screens, purchasing rows, collection, refining, starting an operation and selling selected cargo. Global instant-buy/sell keys from the first test have been replaced.
- **Assets/UI/Mining**: bundled fonts and original transparent artwork.

New Event Sheet actions are under **Mining Interface**. Existing Mining Business actions remain available for custom development. The interface uses scene-object references instead of searching for weapon names.

## Assets and licenses

- Barlow Condensed Bold and Medium: [Google Fonts upstream](https://github.com/google/fonts/tree/main/ofl/barlowcondensed), SIL Open Font License. The full OFL is bundled with the fonts. The font is freely usable but is **not CC0**.
- Press and ore illustrations: generated original transparent UI artwork using the built-in image generator. Sources are preserved in Docs/Concepts/AlienMining/UIAssets. Prompts requested a compact yellow resource-processing press and a faceted copper ore cluster, respectively, without text or frames.
- Panel borders, bars, graph, geometric icons and button states: native scalable drawing, not a flattened screenshot. No third-party icon pack is required by this implementation.

At 1280×720 the modal is 580×660 reference pixels. It scales with the smaller viewport axis and remains centred on wider screens; persistent HUD elements remain anchored to the viewport edges. Balance values and graph samples in demonstration captures are temporary test state, never written to the player's company save.

## Installation and recovery

Plugin version: **3.20.0**. Source is canonical in Plugins/KarlsonDemo and synchronized to palclone/PluginSource. Packages are synchronized to project and installed editor libraries. The desktop Core runtime includes a small public deferred UI drawing API for plugin rendering, using the same portable drawing path as native widgets.

Scene backups are in `.byteengine/Backups/mining-ui-*`; each migration also preserves the prior mining control sheet. Editor/plugin backups are in ByteEngine/UpdateBackups/mining-ui-*. No game export is performed.

Tooling: Tools/MiningPrototype/InstallUi.ps1 installs the package, compatible Core copies, fonts and artwork. `--ui-author` migrates only presentation and control bindings, preserving world objects and operation tuning. `--verify-ui` tests actual Event Sheet/native button interactions in an isolated runtime scene without saving company progress.

## Verified results

- 252 regression checks passed, including production, independent upgrades, partial sales, reserved cargo, price bounds, saved history and modal movement state.
- 39 native UI checks passed using actual Event Sheet key/button input, including no gunfire through modal buttons.
- Installed editor load, play, stop and complete unload passed with exit code 0.
- Actual game screenshots inspected at 1280×720, 1920×1080, 1600×720 and 1280×800. Fixed clipped market price text and unsupported symbols during visual QA.

Project documentation includes Ammo-Press-UI.png, Market-UI.png and Combat-HUD.png.
