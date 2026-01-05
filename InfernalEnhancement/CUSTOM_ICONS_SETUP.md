# Custom Icons Setup - Infernal Enhancement

## ✅ Setup Complete!

The mod is now configured to use custom icons for all three enhancement materials.

## What Was Done:

### **1. Created UIAtlases Folder Structure**
```
7 Days To Die/Mods/InfernalEnhancement/
└── UIAtlases/
    └── ItemIconAtlas/
        ├── README.md (icon specifications)
        ├── infernalBlackStone.png (TO BE CREATED)
        ├── infernalConcentratedStone.png (TO BE CREATED)
        └── infernalCronStone.png (TO BE CREATED)
```

### **2. Updated items.xml**
Changed from vanilla icon with tint to custom icon references:

**Before:**
```xml
<property name="CustomIcon" value="resourceRockSmall"/>
<property name="CustomIconTint" value="FF0000"/>
```

**After:**
```xml
<property name="CustomIcon" value="infernalBlackStone"/>
```

### **3. Removed Color Tints**
- Removed `CustomIconTint` properties since custom icons will have their own colors baked in
- This gives full control over the icon appearance

## Icon Specifications:

### **infernalBlackStone.png**
- **Size**: 80x80 pixels
- **Theme**: Basic enhancement material
- **Design**: Black stone with red glow/veins
- **Rarity**: Common
- **Usage**: +0 to +6 enhancements

### **infernalConcentratedStone.png**
- **Size**: 80x80 pixels
- **Theme**: Advanced enhancement material
- **Design**: Refined black stone with dark red glow
- **Rarity**: Rare
- **Usage**: +6 to +10 enhancements

### **infernalCronStone.png**
- **Size**: 80x80 pixels
- **Theme**: Protective material
- **Design**: Polished gem with golden glow
- **Rarity**: Very Rare
- **Usage**: Prevents regression on failure

## Design Guidelines:

### **Visual Progression:**
```
Black Stone          →  Concentrated Stone  →  Cron Stone
(rough, red)            (refined, dark red)     (gem, gold)
Common                  Rare                     Very Rare
```

### **Color Schemes:**
- **Black Stone**: Black base + bright red (`#FF0000`)
- **Concentrated Stone**: Black base + dark red (`#8B0000`)
- **Cron Stone**: Black base + gold (`#FFD700`)

### **Style Consistency:**
- All three should look like they belong to the same material family
- Progressive refinement from rough to polished
- Dark, infernal aesthetic throughout
- Subtle glowing effects

## How It Works:

### **Game Engine:**
7 Days to Die automatically loads PNG files from `UIAtlases/ItemIconAtlas/` and adds them to the item icon atlas. When an item has:
```xml
<property name="CustomIcon" value="infernalBlackStone"/>
```

The game looks for `UIAtlases/ItemIconAtlas/infernalBlackStone.png` in the mod folder.

### **Fallback Behavior:**
If the PNG file doesn't exist, the game will:
1. Try to find a vanilla icon with that name
2. If not found, display the "missing icon" placeholder (purple/black checkerboard)

## Current Status:

### **✅ Completed:**
- [x] Created UIAtlases/ItemIconAtlas folder
- [x] Updated items.xml with custom icon references
- [x] Removed color tints (no longer needed)
- [x] Created README with icon specifications

### **⏳ Pending:**
- [ ] Create infernalBlackStone.png (80x80)
- [ ] Create infernalConcentratedStone.png (80x80)
- [ ] Create infernalCronStone.png (80x80)

## Next Steps:

### **Option 1: Create Icons Yourself**
1. Use Photoshop, GIMP, Paint.NET, or Aseprite
2. Create 80x80 PNG files with transparency
3. Follow the design guidelines in `UIAtlases/ItemIconAtlas/README.md`
4. Save files to `UIAtlases/ItemIconAtlas/` folder

### **Option 2: Use AI Image Generation**
1. Use DALL-E, Midjourney, or Stable Diffusion
2. Prompt: "80x80 pixel art icon of a [description] on transparent background"
3. Resize to exactly 80x80 pixels
4. Save as PNG with transparency

### **Option 3: Modify Existing Icons**
1. Extract vanilla icons from the game files
2. Modify colors and add effects
3. Save as new PNG files

## Testing:

Once you create the PNG files:
1. Place them in `UIAtlases/ItemIconAtlas/` folder
2. Launch the game
3. Check inventory - the stones should show custom icons
4. If you see purple/black checkerboard, the PNG file is missing or incorrectly named

## File Naming:

**IMPORTANT:** File names are case-sensitive and must match exactly:
- ✅ `infernalBlackStone.png`
- ❌ `InfernalBlackStone.png`
- ❌ `infernalblackstone.png`
- ❌ `infernal_black_stone.png`

## Reference:

See `UIAtlases/ItemIconAtlas/README.md` for detailed icon design specifications and style guidelines.

