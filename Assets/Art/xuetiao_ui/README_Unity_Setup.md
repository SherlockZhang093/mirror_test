# Five-Segment Health Bar UI — Unity Prefab Pack

## Art direction
Dark fantasy pixel art / gothic ornament / cool purple-blue palette /
obsidian-like frame / crystal energy / restrained glow / high readability.

This version deliberately avoids mirror and reflective-glass styling.

## Recommended Unity import settings
Select all PNGs in `Sprites`:
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 100
- Mesh Type: Full Rect
- Filter Mode: Point (no filter)
- Compression: None
- Generate Mip Maps: Off

## Recommended prefab layout
Use `HB_OuterFrame_5Slot.png` as the fixed 5-slot background.
Keep the existing five `Life Block` objects.

Each Life Block should contain:
1. Empty
2. Fill
3. Frame
4. HitFlash
5. Optional HitSpark

The supplied `PlayerHealthBarUI.cs` expects the five `Fill` Images
and five optional `HitFlash` Images from left to right.

## Suggested sizes
At 100%:
- OuterFrame: 816 × 152 px
- Block Frame: 160 × 88 px
- Fill / Empty: 120 × 48 px

For a compact HUD, scale the whole prefab uniformly to 0.45–0.65.

## Files
- HB_OuterFrame_5Slot.png: one-piece outer background for a fixed five-slot bar
- HB_Block_Frame.png: border for a single life block
- HB_Block_Empty.png: dark recessed background
- HB_Block_Fill_Normal.png: normal purple energy fill
- HB_Block_Fill_Hit.png: short damage flash
- HB_Block_Fill_Low.png: low-health fill
- HB_EndCap_Left / Right.png: optional modular end caps
- HB_Center_Ornament.png: optional central decoration
- HB_HitSpark_01~03.png: three-frame hit spark
