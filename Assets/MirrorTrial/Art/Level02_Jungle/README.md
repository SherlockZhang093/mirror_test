# Level 02 Jungle Art Pack

第二关美术基线：成熟的手绘 / 赛璐璐融合风格，非像素画。主色为潮湿的青绿丛林、暖灰遗迹石材和青色镜面魔法；玩家继续复用第一关的白色方形头部、灰色身体与青色剑，保证角色辨识度不变。

## 场景分层

- `Background/jungle_sky_far_v1.png`：室外天空、群山和最远层丛林，建议排序层级 -100。
- `Background/jungle_midground_v1.png`：透明的中远景遗迹和树冠，建议排序层级 -90 至 -72。
- `Background/temple_interior_v1.png`：下落进入神庙后的室内背景。
- `Vegetation/foreground_left_v1.png`、`foreground_right_v1.png`、`canopy_strip_v1.png`：镜头边缘前景遮框，建议排序层级 -10 至 20，不参与碰撞。

## 可行走遗迹模块

- `Platforms/platform_long_v1.png`、`platform_medium_v1.png`、`platform_pillar_v1.png`：主要平台外观。
- `Platforms/ruin_column_v1.png`、`broken_arch_v1.png`、`root_support_v1.png`：支撑和环境叙事构件。
- `Platforms/breakable_floor_v1.png`：从追逐段落落入神庙内部的可破坏地板。
- `Platforms/temple_arch_v1.png`：神庙入口和战斗房间前景框。

美术贴图只负责画面；实际碰撞继续使用第一关的隐藏 BoxCollider2D / EdgeCollider2D 结构，避免贴图轮廓影响手感。

## 机关与奖励

- `Mechanisms/arrival_portal_v1.png`：第一关出口连接到第二关入口。
- `Mechanisms/drawbridge_upright_v1.png`、`drawbridge_lowered_v1.png`、`pulley_v1.png`、`counterweight_v1.png`：配重吊桥微解谜。
- `Mechanisms/mirror_gate_v1.png`：靠近关卡末段的镜门，不应在入口处立即可见。
- `Mechanisms/bow_reward_v1.png`、`bow_shrine_v1.png`：Boss 战后获得远程能力的视觉焦点。
- `Mechanisms/temple_torch_v1.png`：室内暖色节奏光源。

## 水体、植被和特效

- `Water/shallow_water_v1.png`、`water_basin_v1.png`：入口浅水与低处水池。
- `Water/waterfall_narrow_v1.png`、`waterfall_wide_v1.png`：瀑布庭院。
- `Vegetation/fern_cluster_v1.png`、`broadleaf_cluster_v1.png`、`hanging_vines_v1.png`、`root_arch_v1.png`：重复组合时需调整缩放和翻转，避免平铺感。
- `FX/arrow_projectile_v1.png`：Boss 箭矢外观。
- `FX/mirror_afterimage_v1.png`：Boss 飞行 / 瞬移残影。
- `FX/humid_mist_v1.png`：水边和瀑布的局部湿雾。
- `FX/breakable_crack_v1.png`：破坏地板前的裂纹提示。

## 路线对应

传送门入口 → 浅水探索 → 阶梯攀升 → 配重吊桥 → 远程敌人追逐 → 破地板下落 → 神庙内部 → 瀑布庭院 → 镜门 → 镜像弓手 Boss → 弓箭奖励。

`*_chroma_v1.png` 和 `*_sheet_v1.png` 是源资产表与处理后的整表，搭建场景时优先使用已经拆分的独立 PNG。

Unity 会自动通过 `Level02JungleArtImporter.cs` 将本目录下的图片设为单张 Sprite、100 PPU、双线性过滤、关闭 Mipmap、Clamp 边缘和透明通道。
