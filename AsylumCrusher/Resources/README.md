# Asylum Crusher - Unity Asset Bundle

Place your custom Unity asset bundle for the Crusher in this folder.

Recommended naming:
- File: `AsylumCrusher.unity3d`
- Prefab inside bundle: `crusherstation` (lowercase)

Then update the Model property in Config/blocks.xml to point at your asset:

Example:
#@modfolder:Resources/AsylumCrusher.unity3d?crusherstation

Notes:
- The path is relative to this mod folder using @modfolder.
- Ensure the prefab has appropriate colliders and scale.
- Adjust `ModelOffset` and `MultiBlockDim` in blocks.xml if the model sits too high/low or has a different footprint.
- Keep `Shape` set to `ModelEntity`.

