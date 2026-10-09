# General physics and Construction ownership

General scene physics owns enabled `Rigidbody3D` components and their ancestor-attached colliders. `DistanceJoint3D` and `HingeJoint3D` resolve GUID targets only within that scene's general-physics world.

Construction vehicles retain their specialized Bepu simulation. This roadmap does not swap a vehicle's solver, copy Bepu body handles into general joints, or simulate the same transform in both worlds. Specialized articulated vehicles do not acquire general dynamic rigid bodies implicitly.

A body must have one simulation authority. Use a general kinematic body only as an explicit one-way collision proxy for an externally driven body: copy its pose and velocity before the general tick; the general solver cannot push the external simulation back. That proxy's contacts are therefore one-way. Do not connect joints across the boundary or interpret a general constraint's force as a Construction link load.

Terrain can be shared as immutable input data while each backend owns its collision representation. Visual sand deformation remains independent of traction/contact support unless a backend explicitly implements that coupling.

General mesh support includes static triangle surfaces and validated closed convex dynamic hulls, with separate collision data from rendering LODs. Hulls are capped at 128 vertices and 256 triangles. Open/concave hulls report invalid geometry; arbitrary dynamic triangle soups are unsupported. Swept boxes use conservative enclosing spheres; relative-motion and rotational CCD are not promised.

Checks cover opt-in angular stacks, thin-wall high-speed collision, triangle slopes, moving kinematic support, capsule/convex/mesh queries and articulated joints. These checks validate the general solver and do not claim equivalent Construction behavior.
