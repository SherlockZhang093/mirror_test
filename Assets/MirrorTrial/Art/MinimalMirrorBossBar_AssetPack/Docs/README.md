# Minimal Mirror Boss Bar Asset Pack

Style target:
- ultra-minimal mirror language
- thin lines and rectangles
- subtle crack texture
- white / blue-gray / small cold-blue accent
- no complex decorative frame
- suitable for center-bottom boss HP bar in a boss fight

Included sprites:
- BossBar_OuterFrame.png
- BossBar_Fill_Normal.png
- BossBar_Fill_Empty.png
- BossBar_Fill_Hit.png
- BossBar_NameTick_Left.png
- BossBar_NameTick_Right.png
- BossBar_HitSpark_01~03.png
- BossBar_Preview.png
- BossHealthBarUI.cs

Unity import settings:
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Filter Mode: Point (no filter)
- Compression: None
- Mip Maps: Off

Recommended setup:
1. Put the whole BossHealthBar under Canvas.
2. Anchor preset: middle-bottom.
3. OuterFrame is the full border image.
4. FillEmpty is placed under Fill.
5. Fill uses Image Type = Filled.
6. Fill Method = Horizontal.
7. Fill Origin = Left.
8. Control with fillImage.fillAmount = currentHp / maxHp.

Recommended screen placement:
- Anchor: bottom center
- Pos Y: 80~140 px above the bottom edge
- Width can be reduced/scaled depending on your game camera framing
