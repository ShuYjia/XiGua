# 西瓜小课堂 Watermelon Science

## 版本管理

本项目使用 Git 做版本管理（main 分支），每次修改完成后提交，可随时回滚：

```bash
git log --oneline      # 查看提交历史
git checkout <commit>  # 回滚到某个版本
```

基线提交 `bb93561` 为 ModulePanel_2「生长过程」优化前的完整状态；Library/Temp/Logs 等目录已按 Unity 标准 `.gitignore` 排除。

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
- `Assets/Scripts/WatermelonAnatomyController.cs`：认识西瓜模块的视图切换、结构标签和说明内容
- `Assets/Art/watermelon_whole.png`：本地整瓜插画
- `Assets/Art/watermelon_cross_section.png`：本地剖面西瓜插画
- `Assets/Scripts/WatermelonGrowthController.cs`：生长过程模块的阶段导航、上/下一阶段、插画切换、淡入动画与边界禁用
- `Assets/Scripts/WatermelonNutritionController.cs`：营养主题切换、数据卡片和比例图
- `Assets/Editor/WatermelonProjectSetup.cs`：可重复运行的编辑器菜单工具，生成整套 UI 原型
- `tools/generate_growth_art.py`：生长过程三张程序绘制插画的生成脚本（Python + Pillow，可重复运行）

## 当前功能

- 认识西瓜：完整外形 / 剖面结构切换；果皮、果肉、种子、瓜瓤标签说明。
- 生长过程：左侧四阶段导航（编号 + 名称 + 选中高亮），右侧大图展示区显示当前阶段插画、阶段号、标题、说明与进度；支持上一阶段 / 下一阶段（首末阶段自动禁用），切换时插画淡入；重新进入模块重置为第一阶段。
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
- 生长过程模块已验证：四阶段切换、上/下一阶段在首末阶段禁用、重新进入模块重置为第一阶段、返回首页、插画淡入动画在切换时即停旧动画。
- 将 Game 视图设为 1920×1080 检查布局，并用常见窗口比例检查缩放（CanvasScaler 参考分辨率 1920×1080，内容等比缩放）。
- Windows 构建时把 `Assets/Scenes/SampleScene.unity` 加入 Build Settings 的 Scenes In Build，选择 Windows 目标平台后执行 Build。

## 素材来源

- `Assets/Art/watermelon_whole.png`、`Assets/Art/watermelon_cross_section.png`：本项目程序生成的教学插画（扁平风，认识西瓜模块使用）。
- `Assets/Art/growth_stage1_germination.png`（种子萌发）、`growth_stage2_seedling.png`（幼苗生长）、`growth_stage3_flowering.png`（开花授粉）：本项目程序生成的教学插画，由 `tools/generate_growth_art.py`（Python + Pillow，3 倍超采样抗锯齿）绘制，画风与上述插画统一；重新生成：`python tools/generate_growth_art.py`。
- `Assets/Art/growth_stage4_mature.jpg`（果实发育与成熟，完整整瓜照片）：CC0 1.0 公共领域贡献作品。来源：rawpixel（经 Openverse 检索），作者 Scott Webb，原图页 https://www.rawpixel.com/image/430495/free-photo-image-watermelon-pink-agriculture ，许可 https://creativecommons.org/publicdomain/zero/1.0/ 。已下载为本地文件导入 Unity，运行时不依赖网络。
- 全部图片均保存在 `Assets/` 下并在 Unity 中设为 Sprite（`preserveAspect` 开启），不依赖运行时网络加载。如后续替换为外部图片，应继续保存在 `Assets/` 子目录，并在本 README 记录来源和授权信息。

