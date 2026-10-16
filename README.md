# 西瓜小课堂 Watermelon Science

## 当前阶段

阶段四已完成：`认识西瓜`、`生长过程` 和 `营养知识` 三个模块已经具备独立的 UGUI 内容与交互。项目继续使用一个场景、多 Panel 的结构，默认启动首页。

## 环境要求

- Unity 2022.3.34f1c1（当前项目版本）
- Windows Standalone（目标平台：StandaloneWindows64）
- UGUI（`com.unity.ugui`）
- 不依赖第三方 UI 框架或付费插件

## 项目结构

- `Assets/Scenes/SampleScene.unity`：当前启动场景
- `Assets/Scripts/ModuleNavigationController.cs`：单场景 Panel 显隐、模块跳转、返回首页和退出程序
- `Assets/Scripts/MainMenuController.cs`：首页展示层与装饰插画轻微浮动动画
- `Assets/Scripts/WatermelonAnatomyController.cs`：认识西瓜模块的视图切换、结构标签和说明内容\n- `Assets/Art/watermelon_whole.png`：本地整瓜插画\n- `Assets/Art/watermelon_cross_section.png`：本地剖面西瓜插画
- `Assets/Scripts/WatermelonGrowthController.cs`：四阶段生长时间线和科普说明
- `Assets/Scripts/WatermelonNutritionController.cs`：营养主题切换、数据卡片和比例图
- `Assets/Editor/WatermelonProjectSetup.cs`：可重复运行的编辑器菜单工具，生成整套 UI 原型

## 当前功能

- 认识西瓜：完整外形 / 剖面结构切换；果皮、果肉、种子、瓜瓤标签说明。
- 生长过程：种子萌发、幼苗生长、开花授粉、果实发育与成熟四阶段切换，并显示阶段插画和说明。
- 营养知识：水分、糖类、能量三个主题切换，显示教学近似值、计量单位、解释文字和比例示意图。
- 首页与模块导航：四张主题卡片、返回首页、退出程序；模块之间互斥显示。

## 生成或恢复 UI 原型

1. 打开 `Assets/Scenes/SampleScene.unity`。
2. 在 Unity 菜单选择 `Watermelon Science > Create Phase 2 Prototype`。
3. 工具会删除并重建名为 `WatermelonClassroom` 的生成根对象，不会删除项目中的其他有效文件。
4. 点击 Play，首页默认显示；点击前三张主题卡片测试已完成模块，第四张仍为下一阶段小游戏占位页。
5. 各模块的“返回首页”按钮会清理当前模块状态并显示首页，再次进入时恢复默认内容。

## 验证与构建

- 在 Unity Console 中确认无编译错误。
- Play 模式下测试首页、认识西瓜、生长过程和营养知识的按钮交互。
- 验证同一时间只有一个 Panel 可见，返回首页后再次进入模块时状态恢复。
- 将 Game 视图设为 1920×1080 检查布局，并用常见窗口比例检查缩放。
- Windows 构建时把 `Assets/Scenes/SampleScene.unity` 加入 Build Settings 的 Scenes In Build，选择 Windows 目标平台后执行 Build。

## 素材来源

当前阶段使用 Unity 内置 Arial 字体、程序生成的 UGUI 图形，以及 `Assets/Art/` 中的本地西瓜 PNG 插画。PNG 为本项目生成的教学插画，不依赖网络加载；如后续替换为外部图片，应继续保存在 `Assets/` 子目录，并在 README 中记录来源和授权信息。

