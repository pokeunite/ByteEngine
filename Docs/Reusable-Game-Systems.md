# Reusable game systems

These systems belong to ByteEngine.Core and do not depend on Dune Company.

## Speedometer component

Add Speedometer alongside a UI Widget for an optional dial, or alongside UI Text for a readout. Set ReadoutObject to the name of a separate text object if needed. Select km/h, mph or m/s. MaximumSpeed is in the selected display unit. Input through SetSpeed is always metres per second, including negative reverse speed.

For an animated dial set DialPathPrefix, DialPathSuffix and DialFrameCount. For example a prefix of Assets/HUD/dial- and 61 frames selects dial-00.png through dial-60.png. Authored UI widgets control layout and styling; the component supplies values.

For ordinary moving objects enable MeasureTargetMotion and set TargetObject. For physics-based vehicles, provide the actual physics speed through SetSpeed instead: teleporting a target otherwise counts as movement.

## Minimap component

Add Minimap alongside the UI Widget used as the player marker. Author the background map separately so each game can choose its appearance. Set TargetObject, WorldCenter and WorldSize (world X/Z), plus MapOffset and MapSize in the marker's UI parent coordinates. WorldCenter is the map's center; WorldSize is its full extent. InvertZ supports map images drawn with the opposite vertical direction. ClampToMap keeps the marker on the map edge.

Custom movement systems can call SetPosition(worldPosition). WorldToMap is also available for objective and enemy markers; use a separate marker/component for each target. This component maps markers onto an existing image; it does not render a live overhead camera or implement fog of war.

## Native save/load service

GameSaveService is a shared engine API built on GameSaveStorage. Construct it with the game's project root. Runtime saves retain the engine's project/build isolation; browser saves use the existing browser persistence infrastructure. FromDirectory supports callers that already have an engine-resolved save directory.

Save<T>/Load<T> handle JSON data. SaveText/LoadText support arbitrary game formats. Exists and Delete manage slots. Relative keys such as slots/slot-1.json support multiple saves. Writes stage a unique temporary file and replace the destination on success. Keys cannot use rooted paths or parent traversal outside the save root. Disk/JSON failures are reported as exceptions to the caller; a game should display an appropriate failure message.

Games choose their own saved data schema, slots, reset policy and migration rules. The service does not automatically serialize an entire scene or persist arbitrary runtime components. Dune Company's blueprint save/load code uses this shared service while keeping its existing data format.

## Other reusable candidates from Dune Company

Vehicle assembly/attachment rules, construction undo/redo, blueprint libraries and thumbnail rendering, tutorial guidance, objective tracking, rewards/currency, tow/winch mechanics, camera obstruction cutaways and vehicle feedback could also serve other games. They remain game-plugin implementations today, rather than native engine systems. Interactive terrain lives in the separate DesertTerrain plugin.
