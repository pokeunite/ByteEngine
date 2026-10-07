# Goblin Scraper 0.4.3: tuning controls and joint stability

Reopen the project to load the updated installed plugin.

## Tuning controls

Hold and drag a tuning bar to change its value continuously. Dragging stays captured across the UI and uses the actual widget rectangle, including saved panel movement and canvas scaling.

Click the number next to a setting to enter an exact value. The first digit replaces the previous value; Backspace edits, Ctrl+A selects the value for replacement, Enter applies and Escape cancels. Number-row and numpad keys work, including decimal point/comma. Values use the setting's existing safe range; out-of-range values are clamped. ON/OFF rows remain click switches. Press Enter before changing tools to apply your typed value.

The six numeric fields are saved native UiWidget objects under Block tuning panel, so their layout can be edited in the scene. Existing toolbar event sheets remain attached.

## Physics changes

Slider mechanisms now have stronger sideways and rotational restraints while preserving axial spring/piston movement. Soft suspension stiffness/damping still control travel along its axis.

Braces have a stronger damped distance constraint and a lower correction-speed limit. They maintain their span while allowing endpoint rotation; they do not weld moving mechanisms solid.

Wheel brake torque was reduced from 10,000 Nm to 600 Nm. The previous brake could overwhelm joints when grip or speed increased. Acceleration, motor torque and grip tuning remain available at their existing ranges. Braking may feel less abrupt.

## Verification

Native checks: held pointer dragging, moved-panel dragging, exact typed value, undo, same-type application and save/reload. Saved authored scene/HUD and deployment checks still pass.

Physics checks: forward/reverse/counter-steering in both axis orientations; moving brace endpoints on suspension, piston, steering and ball joints; four-wheel suspension machines with and without braces at 1x and 4x wheel speed, 600 Nm torque and 1.5 grip; spring-axis movement remains available.

These fixtures test joint stability; unusual or conflicting brace geometries can still restrict a mechanism's movement. A brace across a suspension axis physically restricts its travel.

Editable source: C:/Users/codex/ByteEngine/Plugins/GoblinScrapper.
Package copies: repository PluginLibrary and Downloads/ByteEngine Plugins.
