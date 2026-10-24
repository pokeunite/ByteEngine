# Model viewer and attachment points

Double-click an FBX or GLB in Assets to open its native Model window. The existing model-tile footer still expands sub-assets; double-clicking the tile opens the viewer. The window includes model counts, searchable bones/nodes, materials, animation names and playback controls, importer properties, and a visual attachment-point editor. Static models have a Neutral Shading toggle to make dark materials easier to inspect; it changes preview rendering only.

## Place a muzzle on a gun without bones

1. Open the gun model and click **Add empty point**.
2. Name it **Muzzle**. Leave Parent on **Model root (no bone)**.
3. Use the Move gizmo to place it at the barrel tip. Rotate/Scale are available as well. Use RMB to orbit, MMB to pan, the wheel to zoom, and Frame Socket / F to focus the selected point.
4. Points save automatically as model metadata. This does not modify or rewrite the original FBX/GLB. Every instance of that model can use the same point.
5. On the scene gun's **Weapon Definition**, select **Muzzle Source: Named Model Point**, then select **Muzzle Point: Muzzle** from the picker.

For per-instance placement, create a scene Empty under the gun, position it with the scene gizmo, and select it in **Muzzle Object** instead. An assigned Muzzle Object takes priority; it follows parenting, animation and transforms. Clear that picker to return to the named model point. Scene Object mode with an unresolved object returns no muzzle position; the existing firing fallback remains compatible. Custom local offsets and imported bone/socket modes remain available for older content.

Root points work on static models and on skinned models. Bone-parented points continue to follow the exact current skeletal pose. Position and transform conversions use the model instance root, including import-space corrections in the rendered mesh hierarchy; runtime rotation and scale must match the preview.

Metadata is kept in `.byteengine/ModelSockets/<model GUID>.sockets.json`, shared with the existing character socket editor. Copy that metadata when moving the project manually. No game export is required to use these changes.

The selected point has an independent identity and transform. New points, duplication, renaming, deletion and gizmo movement affect only that point. The model window provides importer controls; after applying an import change it refreshes the preview and points from the reloaded model.

## Readable event summaries

Health/resource HUD actions now describe the source and destination in a sentence. Dash summaries show their two destination widgets. Weapon text summaries show the chosen text format and destination instead of listing every supported placeholder or empty-state option. Full arguments remain editable in Details and stable instruction IDs are retained for compatibility.
