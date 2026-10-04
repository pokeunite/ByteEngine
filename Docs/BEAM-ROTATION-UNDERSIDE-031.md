# Beam orientation and underside camera (Goblin plugin 0.3.1)

R now turns a hovered beam crosswise by selecting a compatible attachment face, rather than only rolling it around its end mount. On an upright beam it uses a pitch turn; on horizontal beams it uses yaw. Other blocks retain the existing connector-axis twist behavior. T cycles beam end/centre mounting faces; Tab still cycles every authored connector. The preview status displays the selected face.

Right-drag can orbit below the machine. Elevation range is -1.35 to +1.35 radians and the actual build camera stays at least 0.15 m above the yard floor. Middle-drag remains pan and scroll remains zoom.

The current game event sheet gained only the T rule; existing driving rules and any user edits were retained. The new plugin package requires reopening the editor/project to replace an already-loaded plugin assembly.

Verification: crosswise rotations on all six master faces retain socket alignment and attachment position. Native game-loop regression checks the R key placement path and the underside camera with a screenshot, plus existing driving/build restore checks.
