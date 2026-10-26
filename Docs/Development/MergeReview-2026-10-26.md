# master → spathering 合并冲突评审

日期：2026-10-26（Asia/Shanghai）。用户已确认 **方案 A**，并要求同步到主分支。本文保留冲突分析及执行记录；仓库实际主分支为 `master`，不另建 `main`。

## 比较范围

仓库 `spathering/WingSingerGame` 当前没有 `main` 分支，默认主分支为 `master`。本次按用户“主分支合入 spathering”的目标比较：

- 目标：`origin/spathering`，`2a9048fd35284f1e200edcd406ee360c4f6b95c4`。
- 来源：`origin/master`，`1a92a8130d337703b06a0030e6b754536d386d7d`。
- 共同基点：`57d2f7a0bc42a732c846157724646bc7ab00e71e`。
- 本地 spathering 与已获取的远端 spathering 一致。

使用 `git merge-tree --write-tree --messages --name-only origin/spathering origin/master` 预演，未改变索引、当前分支和工程源码。截图列出的 6 个路径中，本地 Git 在识别目录迁移后剩下 **2 个需要人工处理的文件**；另外 4 个可以按重命名关系自动承接上游修改，但还要修正文档引用和验证工具的兼容性。

主分支的主要增量是两层视差背景、对应素材与 Shader、背景接线工具，以及 Arduino 交接文档/历史 ZIP。未修改关卡参数 SO、关卡 Prefab、Unity 包依赖或画质设置。

## 两个实际冲突

### 1. Assets/Scenes/BreathDemo.unity

spathering 已加入完整 GameUI、EventSystem、13 页导航数据绑定、关卡目录 SO、可控启动和窗口适配。master 在同一个场景中添加 `Looping Foreground`，并把 `Looping Background` 换为新的天空材质与视差参数。

Git 文本合并将两侧在相邻位置新增的 YAML 对象块混在一起，最后的 SceneRoots 列表也冲突。双方新增对象的 ID 实际没有重复，UI 与背景可以共存，**不应对整个场景只选 ours 或 theirs**。

推荐处理：

- 以 spathering 的场景结构、UI、单场景关卡实例及输入接线为基础。
- 加入 master 的前景对象及其组件，保留原有 GameUI 和 EventSystem 根对象。
- 将现有远景对象更新为 master 的天空材质和参数。
- 使用上游设计：远景滚动倍率 0.65，前景倍率 1.15，透明前景占画面底部高度 55%；角色、纸鹤及收集物位于背景前方。
- 保留当前“首页启动、起飞才激活关卡”的行为和 1920×1080 UI 适配。

已在忽略目录 `Temp/MergeReview/BreathDemo.proposed.unity.txt` 生成仅供评审的场景草案，检查结果在 `Temp/MergeReview/scene-review.json`：

- 承接上游 8 个背景相关对象/组件块，并合并 SceneRoots。
- 其余 spathering 场景对象块保持一致，包括 Arduino 配置。
- 对象 ID 唯一；无新增悬空的场景内引用；GameUI、EventSystem 和前景根对象均存在。
- 这只是静态结构检查，未把草案导入 Unity，也未宣称通过实际运行验证。

### 2. Docs/Handoffs/ArduinoInputContext.md

类型为 add/add：spathering 把旧根目录交接文档迁移到 Handoffs，master 也在此路径新增了交接文档。冲突段主要是脚本和文档路径：新 `Scripts/Input`、`Gameplay/Core`、`Editor/Setup`、`Editor/Validation` 与旧 `Scripts/Gameplay`、`Editor` 路径不一致。

推荐保留 spathering 的新路径和正确链接，保留有价值的上游交接内容。上游 ZIP 是历史源码快照，按历史材料保留并标明用途，不从 ZIP 覆盖当前 Unity 或固件实现。历史硬件测试结论不改写为本次实测。

## 截图中其余四项

| 截图路径 | 合并后的实际路径 | 检查与建议 |
| --- | --- | --- |
| `Assets/Editor/BreathArtSetup.cs` | `Assets/Editor/Setup/BreathArtSetup.cs` | 保留新目录和原 GUID，承接上游双层背景接线与截图同步全部背景层的逻辑。 |
| `Assets/Scripts/Gameplay/LoopingPanorama.cs` | `Assets/Scripts/Gameplay/Presentation/LoopingPanorama.cs` | 保留新目录和原 GUID，承接滚动倍率、高度占比、锚点、原图比例与视差 UV 计算。 |
| `Docs/ArduinoInput.md` | `Docs/Hardware/ArduinoInput.md` | 上游增加的 `Handoffs/...` 相对链接在新目录下会失效，需改为 `../Handoffs/...`。 |
| `Docs/ArtAssets.md` | `Docs/Art/ArtAssets.md` | 保留上游背景更新说明；修正 `ParallaxBackground.md` 的位置/相对链接。建议新背景文档归入 Docs/Art，验证记录归入 Docs/Validation。 |

新增 `ParallaxBackgroundSetup.cs` 建议放入 `Assets/Editor/Setup` 并一同移动 `.meta`，沿用当前工程分类。新增素材、Shader 和材质保留原 GUID 与引用。

## 合并后还需处理的兼容性

1. 当前目录审计工具要求部分迁移脚本源码与旧版本相同。上游对 BreathArtSetup 与 LoopingPanorama 的合理修改会触发该断言，需要明确登记为视差背景的功能扩展，继续检查 GUID 与引用，不能把失败当成合并损坏，也不能关闭全部审计。
2. 上游背景验证器以旧的自动运行玩法为前提；当前从首页启动。其重开/切关测试和背景截图需要按新的导航入口适配，避免测试仅在菜单后方运行或截到 UI 代替背景。
3. 上游更新了 `UserSettings/Layouts/default-2022.dwlt`，本机该文件及其他布局文件也有未提交修改。推荐合并结果保留 spathering 的个人布局版本，本机配置先备份、最终原样恢复，不随功能合并覆盖。
4. 合并后核对文档链接、两层背景材质/Shader、原有 UI 资源和关卡引用，并运行视差背景、正式导航和多窗口布局验证。复用当前 Unity 与 Library，不清缓存或重导入全工程。

## 待用户决定的方案

**推荐 A：保留 spathering 的 UI、目录结构、关卡 SO、窗口适配和输入行为，同时引入 master 的双层视差背景；文档合并并修正路径，本机个人配置保留。**

备选 B：暂时继续显示原单层森林背景；纳入上游视差资源和代码但不在当前场景启用，文档明确启用状态。其他目录迁移与交接文档按上述规则合并。

## 方案 A 执行记录

- 已保护本机 UserSettings 与 Odin 私有配置，合并结果继续保留 spathering 的个人布局版本。
- 已将 master 合入 spathering，按场景对象块保留现有 GameUI、EventSystem、输入与关卡 SO，承接双层背景对象和参数；没有整文件覆盖场景。
- 已采用新目录下的 Arduino 交接文档，保留上游 ZIP 为历史快照，修正 Hardware 与 Art 文档链接。
- 背景工具与 `.meta` 一同归入 `Editor/Setup`；原脚本和资源 GUID 保持不变。目录审计明确登记两项上游功能扩展。
- 背景验证改为通过生产导航启动、重开和进入第二关，使用隔离进度与测试输入；截图同步全部背景层和实际渲染目标，完成后恢复相机/UI。
- 运行验证临时允许后台执行，结束时恢复，避免编辑器失去前台焦点造成动画测试误报；没有修改 Player Settings。
- 视差集成 18 项、正式导航 143 项、多窗口 117 个组合／2273 项、参考画廊及输入/收集逻辑回归结果见下方报告。验证复用现有 Unity 和 Library，无清缓存或全量重导入。

验证证据：[合并引用与素材检查](../Validation/MergeIntegrationValidation.json)、[背景集成运行检查](../Validation/ParallaxBackgroundMergeValidation.txt)、[正式导航](../Validation/GameplayNavigationValidation.json)、[窗口适配](../Validation/UIViewportValidation.json)、[目录与文档审计](../Validation/ProjectOrganizationValidation.json)。

发布方式：正常合并提交后推送 spathering，再将同一合并提交快进到 master，最后拉取核对本地与远端一致；不强制推送。提交及推送前再次核对远端，若来源分支变化则先重新整合。
