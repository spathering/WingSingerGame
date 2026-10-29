# UI 与结构整理验收

## 2026-10-29：硬件配置与 ADC HUD

- 配置抽离到独立 SO，运行时副本与本机 JSON 分离；游戏入口 `Settings > Hardware settings`。
- 新增第 14 个生产 UI 页面，保留原 13 张参考页面；暂停期间可调参、返回后恢复原会话。
- 实际 HUD 显示 ADC 与吸气/默认/呼气，并验证场景旧绑定不会覆盖新文案。
- 输入与配置回归 **171 项**；实际 Unity BLE/硬件设置与 HUD 检查 **144 项**；画廊 Play **51 项**。
- **14 页 × 9 窗口 = 126 组**，**2867 项**布局/文字/控件检查；交付审计 **323 项**；收集逻辑 **151 项**。
- [硬件设置说明](../Hardware/HardwareSettings.md)、[实际硬件页截图](../Design/UI/RuntimePreviews/HardwareSettings_1280x720.png)、[实际 ADC HUD 截图](../Design/UI/RuntimePreviews/LiveHardwareInhale.png)。截图与输入检查使用模拟通知，不代表真实硬件连通。验证写入项目 Temp 内的独立测试 JSON，不修改用户配置或进度。
- 以下为 2026-10-25 / 26 的历史交付记录；当前输入和页面配置以上述入口与最新报告为准。

## 2026-10-25 原参考 UI 与结构交付

| 检查 | 结果 / 范围 |
| --- | --- |
| 参考图 | 13 张，原始内容 SHA-256 匹配 |
| 通用 UI 素材 | 60 项 PNG、官方字体与许可、Sprite 导入及九宫格 |
| UI 页面 | 13 个可编辑 Prefab、7 个共用组件、1 个 UIFlowRoot |
| Unity 构建检查 | 493 项，真实导入/序列化/导航与 Canvas 渲染 |
| 参考画廊 Play Mode | 50 项，按钮回调、滑条、开关、来源返回和单页面激活 |
| 正式导航 Play Mode | 143 项，SO 目录、两关初始可选和实际进入、按钮动画、起飞、暂停/恢复/重开、退出清理、历史 ID、输入练习、存档和设置 |
| 多窗口布局 | 13 页 × 9 种尺寸共 117 个组合、2273 项检查；540 个控件和 1494 处渲染文字边界，含点击及悬停验证 |
| 交付审计 | 321 项，素材哈希、路径、GUID、文档链接、16:9 截图、SO 引用和运行报告 |
| 目录迁移 | 37 条路径；24 个迁移脚本保留 GUID，21 个源码不变，玩家/背景表现与美术接线脚本按已登记功能扩展；原路径下另扩展管理器与关卡表现 |
| 输入回归 | 86 项，通过 |
| 收集物回归 | 151 项，通过；补齐可运行测试项目入口 |

证据：[构建报告](UIBuildValidation.json)、[画廊 Play Mode](UIPlayValidation.json)、[正式导航 Play Mode](GameplayNavigationValidation.json)、[目录与素材审计](ProjectOrganizationValidation.json)、[13 页渲染总览](../Design/UI/Previews/UIScreensOverview.png)、[主场景运行截图](../Design/UI/RuntimePreviews/)。

视觉检查按源图核对层级、文案和主要布局；森林背景为根据参考补全的插画，角色为像素切图与透明蒙版，通用底板由参考色板重建。没有将整张 UI 图当作运行时页面使用。

`BreathDemo` 已接入导航图：菜单启动不运行关卡，起飞后通过已有管理器激活一关，暂停保留会话与 USB 接收，结果存储真实数据。`UIFlowPreview` 继续使用参考示例数据。实际界面统一吸气向上、呼气向下，现有两关以外的卡位保持禁用。未知电量显示 `--`，没有新增 BLE 或无线信号服务。详见 [设计文档](../Design/UI/UIFlowDesign.md)。

现有两关通过 `BreathLevelDefinition` 与 `WingSingerLevelCatalog` SO 关联原有 Prefab 和参数，初始均可选择。准备页名称与时长均读取当前关卡。全部 Button 添加 PrimeTween 悬停、按下和回弹；暂停时使用非缩放时间，禁用/退出清理动画，Reduce Motion 立即取消缩放。配置入口见 [UI 交互与关卡目录](../Development/UIInteractionsAndLevelCatalog.md)。

Canvas 已统一为 1920×1080 基准，页面由 `UIPageViewport` 等比居中适配，背景独立铺满。验证复现了旧配置在 16:9 下的 7 个控件越界；修正后全部检查通过。报告见 [UIViewportValidation.json](UIViewportValidation.json)，对比说明见 [UI 窗口适配](../Development/UIViewportLayout.md)，实际截图见 [ViewportPreviews](../Design/UI/ViewportPreviews/)。正式玩法运行截图现使用 1280×720；早期参考图与原始画廊截图保留原比例。

2026-10-26 已按方案 A 整合主分支的双层背景，同时保留 UI、关卡 SO、输入与窗口适配。新增 [18 项视差/生产导航集成检查](ParallaxBackgroundMergeValidation.txt)，并重跑正式导航、多窗口及参考 UI 验证。输入与收集逻辑分别通过 86、151 项。素材内容、场景引用与路径检查见 [合并验证](MergeIntegrationValidation.json)；冲突处理记录见 [分支合并评审](../Development/MergeReview-2026-10-26.md)。

正式导航验证使用生产接收器与模拟 USB 传输，覆盖 300 mV→吸气上轨、1300 mV→呼气下轨；练习保持时间使用确定性推进，成绩结算使用实际会话模型。测试使用空存档键，不修改用户进度，也不打开物理串口。本次不代表已复测 ESP32 实物连接。音乐和音效音量通过测试 AudioSource 验证，场景尚未配置实际音频资源。

验证使用本工程已有 Library 和 Unity 2022.3.62f2；没有清缓存、改变程序集/平台/渲染档位。主场景新增公共 UI 绑定，预览场景未加入游戏构建列表，关卡实例与资源引用保留。
