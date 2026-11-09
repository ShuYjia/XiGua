# 西瓜小课堂 Watermelon Science

## 版本管理

本项目使用 Git 做版本管理（main 分支），每次修改完成后提交，可随时回滚：

```bash
git log --oneline      # 查看提交历史
git checkout <commit>  # 回滚到某个版本
```

基线提交 `bb93561` 为 ModulePanel_2「生长过程」优化前的完整状态；Library/Temp/Logs 等目录已按 Unity 标准 `.gitignore` 排除。

## 当前阶段

主工程四个模块均已可用：`认识西瓜`、`生长过程`、`营养知识`、`挑选西瓜`。主课堂仍是单场景、多 Panel 结构，默认启动首页；`生长过程` 与 `挑选西瓜` 分别新增独立子游戏入口，通过独立场景运行《瓜田农场》与《瓜里有数》，子游戏内可“返回课堂”回到发起跳转的模块。

## 统一设计基准、西瓜币与礼品兑换

- **分辨率**：主课堂、Farm、Cut 统一以 `1920 × 1080` 为唯一设计基准。`Assets/Scripts/WatermelonResolutionAdapter.cs` 提供 `WatermelonResolution`（`GetScale` / `GetContentRect` / `ScreenToDesign` / `DesignToScreen` / `ApplyAspectFit`）。缩放只由 `CanvasScaler(ScaleWithScreenSize + Expand)` 做一次，内容根节点固定 1920×1080 并居中；16:9 分辨率整体等比缩放，非 16:9 由背景填充留白，不裁切、不拉伸。主课堂内容根为 `ClassroomAspectRoot`，Farm / Cut 为 `WatermelonAspectRoot`。
- **Cut 坐标**：调用处仍写 1440×900 网页稿数值，由 `GuiliUIKit.LegacyX / LegacyY / LegacyWidth / LegacyHeight / LegacyFont` 统一换算到 1920×1080，字号按高度系数等比放大；`At` / `TextAt` / `BoxAt` / `Fixed` / Header / Home / Practice / Challenge / Cut / Result / Modal / Toast 全部走同一套换算。`PlayViewport`、`CutViewport`、相机 `rect` 与屏幕指针共用同一适配器。
- **统一货币**：全项目只显示「西瓜币」，唯一数据源是 `Assets/Scripts/WatermelonCurrencyWallet.cs`。旧农场金币 + 旧碎片（PlayerPrefs 与共享 JSON 取较大值一次）只迁移一次，迁移标记 `pgz_watermelon_currency_migrated_v1`；无旧数据时初始 100。Farm 卖瓜发放 `售价 + 每瓜 2` 西瓜币，Cut 按 0–100 分发放等额西瓜币且每局最多一次；种子、化肥、地块、升级、自动化与礼品兑换全部从统一钱包扣费。`ShardWallet` 保留为纯转发兼容层。
- **礼品兑换（本地订单演示）**：`WatermelonGiftCatalog` 集中配置 4 件礼品的名称/说明/价格/库存，`WatermelonGiftRedemptionService` 按「礼品→启用→库存→姓名→手机号→地址→余额→扣币→扣库存→生成订单号→保存→结果」顺序创建订单，订单号 `WM-YYYYMMDD-XXXX`、状态「待审核」，存本机 `pgz_watermelon_gift_orders_v1`，库存存 `pgz_watermelon_gift_stock_v1`；保存失败回滚西瓜币与库存。主课堂、Farm、Cut 三处入口共用 `WatermelonGiftPanel`。**当前为本地订单演示，订单只保存到本机，不会自动连接支付、物流或真实发货系统；无在线支付、无云端后台、无网络 API、无账号系统。**

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
- `Assets/Scripts/SubgameSceneRouter.cs`：子游戏场景路由，记录返回模块并在返回时恢复
- `Assets/Scenes/Subgames/Farm.unity`：瓜田农场子游戏场景（农场自启动组件挂在场景标记对象上）
- `Assets/Scenes/Subgames/Guayoulishu.unity`：瓜里有数挑瓜子游戏场景（切瓜总控挂在场景对象上）
- `Assets/Subgames/WatermelonFarm/`：农场运行时脚本、`Resources/Art` 美术与只做校验的编辑器工具
- `Assets/Subgames/WatermelonCut/`：切瓜运行时脚本、`Resources/Art`、`Resources/Fonts`、`Models`、`Prefabs` 与运行时冒烟测试
- `tools/generate_growth_art.py`：生长过程三张程序绘制插画的生成脚本（Python + Pillow，可重复运行）
- `Assets/Scripts/WatermelonResolutionAdapter.cs`：统一 1920×1080 设计基准与屏幕/逻辑坐标换算
- `Assets/Scripts/WatermelonCurrencyWallet.cs`：唯一西瓜币钱包、旧金币与碎片一次性迁移
- `Assets/Scripts/WatermelonGiftCatalog.cs`：礼品目录与本机库存
- `Assets/Scripts/WatermelonGiftRedemptionService.cs`：本地订单演示服务（含失败回滚）
- `Assets/Scripts/WatermelonGiftPanel.cs`：三处共用的兑换面板与常驻余额 HUD
- `Assets/Editor/WatermelonCurrencySmokeTest.cs` / `WatermelonGiftSmokeTest.cs`：钱包与礼品自检

## 当前功能

- 认识西瓜：完整外形 / 剖面结构切换；果皮、果肉、种子、瓜瓤标签说明。
- 生长过程：左侧四阶段导航（编号 + 名称 + 选中高亮），右侧大图展示区显示当前阶段插画、阶段号、标题、说明与进度；支持上一阶段 / 下一阶段（首末阶段自动禁用），切换时插画淡入；重新进入模块重置为第一阶段。
- 营养知识：水分、糖类、能量三个主题切换，显示教学近似值、计量单位、解释文字和比例示意图。
- 挑选西瓜：模块说明与“开始挑瓜”按钮，进入《瓜里有数》独立挑瓜玩法（首页、四步教学、九瓜观察、五项记录、依据校验、切瓜评分、换一篮重玩）。
- 生长过程：原有四阶段图文完整保留，模块标题右侧新增“进入瓜田农场”按钮，进入《瓜田农场》独立养成玩法（九块地、三品种、八阶段、农事工具、告警与枯萎、商店/自动化、收获出售、图鉴与日志、离线推进）。
- 首页与模块导航：四张主题卡片、返回首页、退出程序；模块之间互斥显示。两个子游戏内均有“返回课堂”入口，返回后恢复到发起跳转的模块。

## 生成或恢复 UI 原型

1. 打开 `Assets/Scenes/SampleScene.unity`。
2. 在 Unity 菜单选择 `Watermelon Science > Create Phase 2 Prototype`。
3. 工具会删除并重建名为 `WatermelonClassroom` 的生成根对象，不会删除项目中的其他有效文件。
4. 点击 Play，首页默认显示；四张主题卡片分别进入对应模块。
5. 各模块的“返回首页”按钮会清理当前模块状态并显示首页，再次进入时恢复默认内容；子游戏内的“返回课堂”只返回发起跳转的模块，不改变原有首页导航。
6. 生成器只删除并重建 `WatermelonClassroom` 根对象；不会重建子游戏场景或源工程资产。

## 运行方式与入口

1. 打开 `Assets/Scenes/SampleScene.unity`，按 Play：首页默认显示四张主题卡片。
2. 首页 → 02 生长过程：原有四阶段图文继续显示，标题右侧的“进入瓜田农场 ›”进入 `Assets/Scenes/Subgames/Farm.unity`。
3. 首页 → 04 挑选西瓜：显示“开始挑瓜”按钮，进入 `Assets/Scenes/Subgames/Guayoulishu.unity`。
4. 两个子游戏内点“返回课堂”，会回到发起跳转的模块（生长过程 / 挑选西瓜），不是无条件回首页。
5. 也可以单独打开 `Farm.unity` 或 `Guayoulishu.unity` 按 Play：对应场景内的游戏总控组件会自动初始化一次。
6. Build Settings 顺序：`SampleScene`（0，启动场景）、`Farm`（1）、`Guayoulishu`（2）。Windows Standalone 构建时三个场景都要勾选。
7. 主课堂右上角、Farm 与 Cut 顶栏均有常驻「西瓜币 N」与「礼品兑换」入口，三处打开的是同一个本地订单演示面板；测试时请使用虚拟收货信息。
8. 已验证分辨率：`1920×1080`、`1600×900`、`1280×720`、`1920×1200`、`1440×900`、`1366×768`、`1024×768`；16:9 等比缩放，非 16:9 使用居中 16:9 内容区与背景留白。

## 存档、迁移与共享碎片

- 主工程 PlayerSettings 为 `DefaultCompany / My project`，与农场源工程的 `Panggezhuang / 瓜田农场` 命名空间不同，Unity `PlayerPrefs` 不会自动读到源工程旧存档；主工程内首次进入 Farm 会建立新档，旧工程与旧存档保持不变。
- 农场进度存档键为 `pgz_farm_unity_v1`，农场运行中自动保存；离线最多补算 8 小时生长，离线期间不累计“告警未处理”计时。`SaveData.coins` 只作为旧存档兼容字段保留，不再是余额来源。
- 统一钱包键 `pgz_watermelon_currency_v2`，迁移标记 `pgz_watermelon_currency_migrated_v1`。首次初始化读取 `pgz_farm_unity_v1.coins`、`pgz_shard_wallet_v1` 与 `%LOCALAPPDATA%\PanggezhuangGames\shard_wallet.json`，碎片取两处较大值一次（旧 JSON 的 `id` 仍为 `watermelon`），`新余额 = 旧金币 + 碎片`；三者都不存在时初始 100。标记写入后不再重复迁移。
- 重置农场进度只删 `pgz_farm_unity_v1`，**不会重置西瓜币**；`ShardWallet` 的 `Get / Add / Set / Reload / Export` 全部转发统一钱包，仍导出旧格式兼容 JSON。
- 礼品订单键 `pgz_watermelon_gift_orders_v1`、库存键 `pgz_watermelon_gift_stock_v1`，均为本机 PlayerPrefs；订单状态固定「待审核」。
- 场景路由状态只在应用运行期保存，卸载/退出后不写永久存档；重新启动应用会回到主首页。
- 所有自检（钱包、礼品、Farm、Cut）都会先备份、结束后恢复真实农场存档、旧碎片 PlayerPrefs、共享 JSON、钱包余额、迁移标记、订单与库存；测试数据只使用虚拟姓名、虚拟手机号与虚拟地址。

## 验证与构建

- 在 Unity Console 中确认无编译错误；MCP 编译检查要求 Console 中不存在 CS 编译错误。
- Play 模式下验证：首页四入口、模块1/3 原有交互、模块2 进入 Farm 并返回模块2、模块4 进入 Cut 并返回模块4、冷启动仅显示首页、子游戏场景单独启动只初始化一次。
- 编辑器菜单（均为可重复运行）：
  - `Tools/WatermelonFarm/Run Runtime Smoke Test`：种植 → 八阶段生长 → 收获 → 存档往返 → 出售（碎片 +2）→ 离线推进。
  - `Tools/WatermelonCut/Run Runtime Smoke Test`：教学四步 → 九瓜观察 → 依据校验 → 切瓜 → 评分 → 换一篮，并校验四类切面/音色/评分映射。
  - `Watermelon Science/Run Classroom Route Smoke Test`：首页 → 模块2 → Farm → 返回 → 模块4 → Cut → 返回，检查残留总控、重复 EventSystem、主相机丢失。
  - `Watermelon Science/Run Currency Smoke Test`：迁移 80 金币 + 碎片 25 → 105、重复初始化不重复迁移、无旧存档 100、重置农场不重置钱包、消费/收入边界、卖瓜 +82 / 批量 +246、Cut 0/60/85/100 分发币、ShardWallet 兼容层。
  - `Watermelon Science/Run Gift Smoke Test`：礼品目录、余额/库存/姓名/手机号/地址校验、扣币扣库存、订单号唯一、状态「待审核」、重启持久化、保存失败回滚、三处入口。
  - `Watermelon Science/Capture Verification Screenshots`：在 1440×900 与 1920×1080 下截图到 `Logs/VerificationScreenshots/`。
- 测试结果写入 `Logs/*SmokeResult.txt` 与 `Logs/ScreenshotResult.txt`；这些文件属于本机验证产物，不参与构建。
- 将 Game 视图分别设为 1440×900 与 1920×1080 检查布局、中文字体、美术加载与子游戏“返回课堂”按钮可见性。
- Windows 构建：目标平台 StandaloneWindows64，三个场景均在 Scenes In Build 中，`SampleScene` 保持第一位。

## 素材来源

- 子游戏内容来源：`Assets/Subgames/WatermelonFarm/` 与 `Assets/Subgames/WatermelonCut/` 的玩法、数值、文案、美术与字体均来自用户提供的 `WatermelonFarmUnity` 与 `watermelon cut` 源工程，导入时保留原有 `Resources` 相对路径、资源与 `.meta`；源工程本身未被修改。用户提供的调研 Markdown / PDF 仅作为视觉与知识参考，未按网页版式重写主工程。
- 子工程插图、照片与字体的授权/来源信息以各自源工程及其文档为准；本整合未新增未经核实的事实或数据。
- `Assets/Art/watermelon_whole.png`、`Assets/Art/watermelon_cross_section.png`：本项目程序生成的教学插画（扁平风，认识西瓜模块使用）。
- `Assets/Art/growth_stage1_germination.png`（种子萌发）、`growth_stage2_seedling.png`（幼苗生长）、`growth_stage3_flowering.png`（开花授粉）：本项目程序生成的教学插画，由 `tools/generate_growth_art.py`（Python + Pillow，3 倍超采样抗锯齿）绘制，画风与上述插画统一；重新生成：`python tools/generate_growth_art.py`。
- `Assets/Art/growth_stage4_mature.jpg`（果实发育与成熟，完整整瓜照片）：CC0 1.0 公共领域贡献作品。来源：rawpixel（经 Openverse 检索），作者 Scott Webb，原图页 https://www.rawpixel.com/image/430495/free-photo-image-watermelon-pink-agriculture ，许可 https://creativecommons.org/publicdomain/zero/1.0/ 。已下载为本地文件导入 Unity，运行时不依赖网络。
- 全部图片均保存在 `Assets/` 下并在 Unity 中设为 Sprite（`preserveAspect` 开启），不依赖运行时网络加载。如后续替换为外部图片，应继续保存在 `Assets/` 子目录，并在本 README 记录来源和授权信息。

