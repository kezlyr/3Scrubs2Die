# Unity3D Assets for Asylum Entities - Revised

## Asset Structure
This folder will contain Unity3D asset bundles for asylum entities.

## Naming Convention
- Use descriptive names for asset bundles
- Follow pattern: AsylumEntityType.unity3d
- Prefab names should match entity class names

## Shader Requirements
All materials should use:
- Enhanced KHZ Shader for body materials (MatColor support)
- Clothes Electric Shader for clothing materials (electrocution only)

## Asset Guidelines
1. **Models**: High quality but optimized for performance
2. **Textures**: Appropriate resolution for game distance
3. **Materials**: Proper shader assignment and texture mapping
4. **Prefabs**: Correct naming and component setup
5. **LODs**: Consider LOD groups for performance

## Electrocution Support
Ensure all materials have:
- Proper emission maps (white where glow should appear)
- Electric FX textures assigned (if using electrocution)
- Correct shader properties set

## Performance Notes
- Use GPU instancing compatible shaders
- Optimize texture sizes appropriately
- Consider LOD systems for complex models
- Test with multiple entities spawned

## Asset List
*To be populated as assets are created*
