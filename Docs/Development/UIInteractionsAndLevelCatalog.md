# UI 交互与关卡目录

更新：2026-10-25。实际入口为 `Assets/Scenes/BreathDemo.unity`。该场景启动在首页，选关后激活场景中已经存在的关卡 Prefab 实例。

## 按钮效果

`UIInteractionTween` 位于 `Assets/Scripts/UI/Controls`，已添加到 13 页 UI 的全部 Button，包括共用按钮、关卡卡片、图标和文字按钮。

| 状态 | 效果 | 默认时长 |
| --- | --- | --- |
| 悬停 / 键盘选中 | 放大至 1.035 倍 | 0.12 秒 |
| 鼠标左键按下 / 键盘提交 | 缩小至 0.96 倍 | 0.07 秒 |
| 松开 | OutBack 回弹至悬停或原始大小 | 0.16 秒 |
| 离开 / 取消选中 | 恢复原始大小 | 0.12 秒 |

使用现有 PrimeTween 的 `Tween.Custom`，按非缩放时间推进，因此暂停菜单在 `Time.timeScale=0` 时仍可交互。缩放保持视觉中心不变，不修改美术布局的 pivot。禁用按钮、关闭页面或销毁对象会停止其动画并恢复原始位置与大小；动画不延迟按钮原有的点击事件。

设置中的 Reduce Motion 会立即取消可选缩放，并继续保留 Button 原有的颜色/精灵状态反馈。每个按钮可在 Inspector 调整上述倍率和时长；共用按钮在 `Assets/Prefabs/UI/Shared` 中调整后可向实例继承。

## 现有关卡 SO

| 可选关卡 | 定义 SO | 原有玩法配置 | 原有 Prefab |
| --- | --- | --- | --- |
| First Flight | `Level_001_FirstFlight_Definition.asset` | `Level_001_FirstFlight.asset`：吸气 3 秒、呼气 4 秒、45 秒会话 | `Level_001_FirstFlight.prefab` |
| Slow Breath | `Level_002_SlowBreath_Definition.asset` | `Level_002_SlowBreath.asset`：吸气 4 秒、呼气 6 秒、60 秒会话 | `Level_002_SlowBreath.prefab` |

SO 位于 `Assets/Settings/Levels`，Prefab 位于 `Assets/Prefabs/Levels`。两关均设置 `availableFromStart=true`，初次进入即可选择；未配置的参考卡位保持禁用。

`BreathLevelDefinition` 负责关卡身份与展示：`levelId`、标题、描述、缩略图、Prefab、呼吸参数 SO、初始开放状态，以及仅供追溯的源场景路径。`BreathLevelSettings` 继续负责呼吸节奏、时长、移动和收集参数。`WingSingerLevelCatalog.asset` 统一保存定义列表及选关顺序。

选关页、准备页和结果页从定义与参数 SO 读取名称和数值；Start Flight 调用 `BreathLevelManager.SwitchLevel`，进入匹配的已有关卡实例。`sourceScenePath` 只记录制作来源，不在运行时调用场景加载。MainScene 保留现有基础环境；本轮可玩内容来自 BreathDemo 的两个关卡实例。

成绩记录新增稳定 `levelId`，调整目录顺序后历史重玩仍定位同一关卡；旧成绩未带 ID 时按原编号兼容。不要在发布后随意修改已有定义的 ID。原来的进度字段仍保留；对未来设置为非初始开放的关卡，仍按目录顺序使用解锁进度。

## 后续添加关卡

1. 制作独立 `BreathLevel` Prefab，配置其 `BreathLevelSettings`、出生点、引导和收集资源。
2. 通过 Create → WingSinger → Level Definition 创建定义 SO，填写唯一 ID，引用该 Prefab 和它使用的同一个参数 SO。
3. 将定义加入 `WingSingerLevelCatalog`；当前选关布局最多容纳 12 个定义。
4. 把 Prefab 实例预放到 BreathDemo 的 LevelRoot 下。每个关卡根对象相互独立，不添加另一套公共相机、玩家、输入接收器或 EventSystem。
5. 在保存场景后运行 `WingSinger > UI > Apply Button Effects and Level Catalog`，按目录匹配已有实例；没有对应实例、重复 ID 或参数不一致时会明确报错。
6. 运行 `WingSinger > UI > Validate Gameplay Navigation`，检查首次进入、切换、重开、退出清理与暂停恢复。

接线工具会保留已有 UI 场景实例和布局覆盖，只添加缺失的交互组件与数据绑定；执行前要求保存场景及 Prefab Stage，不会丢弃未保存修改。首次执行前的主场景备份位于 `Temp/NavigationBackup/BreathDemo.before-level-catalog.unity`。

验证证据见 [正式导航报告](../Validation/GameplayNavigationValidation.json) 和 [运行截图](../Design/UI/RuntimePreviews/)，包括两关进入、实际 SO 参数、按钮暂停时动画、恢复/禁用/切页清理、Reduce Motion 与历史 ID 映射。串口输入使用模拟传输，不代表新增实物测试。
