# 《镜中试炼》项目文档索引

本目录只保存项目文档。Unity 运行时脚本、预制体、配置和美术资源统一放在 `Assets/MirrorTrial`。

## 领域分类

| 目录 | 负责内容 | 首要入口 |
| --- | --- | --- |
| [`Combat`](Combat/README.md) | 玩家技能、连招、敌人、受击、击飞及战斗编辑器 | `CODEX_SKILL_EDITOR_HANDOFF.md` |
| [`3C`](3C/README.md) | Character、Camera、Control、移动手感和玩家框架 | `MirrorTrial_Player3C_Design.md` |
| [`3C/Camera`](3C/Camera/README.md) | 镜头跟随、演出、震动和 Cinemachine 接入规范 | `CameraShake_Integration_Guide.md` |
| [`UI`](UI/README.md) | HUD、生命值显示、交互反馈及 UI 系统 | `生命值与生命资源系统.md` |
| [`Level`](Level/README.md) | 关卡系统、编辑器、LevelManager、关卡美术规范 | `MirrorTrial_LevelSystem_CurrentOverview.md` |
| [`Project`](Project/README.md) | 总体策划、美术方向、实现计划和跨领域资料 | `MirrorTrial_iWiki_Rewrite.md` |
| [`Diagrams`](Diagrams/) | 系统图、流程图及导出图片 | `*.svg` |

## 归档规则

1. 文档按主要维护责任归档；跨领域内容只保留一个主文件，其他分类通过索引引用。
2. 相机属于 3C，所有相机专项文档放在 `3C/Camera`。
3. 战斗中的镜头反馈实现仍以 `3C/Camera/CameraShake_Integration_Guide.md` 为唯一技术规范。
4. 关卡中的战斗区、刷怪点归关卡文档；敌人行为、战斗反馈和击飞规则归战斗文档。
5. 全局策划、阶段计划和美术总纲放在 `Project`，不复制到各领域目录。

## 新增文档命名建议

- 设计或需求：`<系统名>_Design.md` / `<系统名>_PRD.md`
- 当前实现：`<系统名>_CurrentOverview.md`
- 操作说明：`<系统名>_UsageGuide.md` / `<系统名>_Manual.md`
- 接入规范：`<系统名>_Integration_Guide.md`

