# Inputsystem DOTS
This is a Unity package for generating necessary ECS components and systems to work with the new Unity Player InputSystem directly from DOTS.
## Features
 - Automatically generate easy to work with components for all the supported input control value types.
 - Supports untyped input control values.
 - Converts value types into their Unity.Mathematics counterparts if available (Vector2 -> float2, Vector3 -> float3, Quaternion -> quaternion)
 - Filter out inputs on components via control schemes.
 - Automatically regenerates assembly if there are any changes in the source InputActionAsset