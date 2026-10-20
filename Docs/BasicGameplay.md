# 基础玩法与示例关卡

## 运行方式

使用 Unity 2022.3.62f2 打开项目，打开 `Assets/Scenes/BreathDemo.unity`，点击 Play。原有 `MainScene.unity` 保留；示例场景作为首个构建场景。

- 按住 W：吸气，前往上轨道。
- 按住 S：呼气，前往下轨道。
- 松开两键或同时按下：回到中轨道，不计分。
- R：重开当前关卡。
- N：切换下一个关卡，两关循环。
- 1 / 2：直接进入对应示例关卡。

“按住控制、松开回中间”是本次原型对输入方式的约定。未来可根据传感器输出调整，不影响标准路线与计分模型。

## 核心规则

玩家从中轨道出发。发光精灵先出发，留下上、下轨道交替的发光路线；预览时间结束后玩家自动向右移动，精灵继续以相同速度前进。正交摄像机从正面观察场景，水平跟随玩家；切换轨道时相机不会上下追随。

路线按空间位置确定：先在中轨道行进 `preparationSeconds` 秒，再用 `guideTransitionSeconds` 秒平滑进入上轨道。随后上轨道持续吸气时长，平滑移到下轨道，下轨道持续呼气时长，再平滑返回上轨道，循环往复。换轨使用缓入缓出的曲线，精灵与留下的光线采样同一条连续路线，不会瞬间跳变。

精灵与玩家分别根据自己的横向位置采样标准路线。玩家应跟随自己所在位置的发光路线，而非精灵当前所处轨道。当前画面只保留背景、角色与发光引导，已移除顶部、底部、轨道文字及结算 UI 和三条固定黑色轨道线；上、中、下三条逻辑轨道继续用于移动和计分。

正确轨道的停留时间乘以每秒分数得到得分。预览、准备阶段、中轨道、错误轨道、引导路线曲线段和玩家换轨过渡期间不计分。吸气与呼气时长表示在对应轨道的完整停留时间，曲线的移动时间另加。分数按持续时间在状态模型中累计；不会因帧率不同重复加分，结束帧只累计总时长内的部分。

游戏总时长只计算玩家开始移动后的时间，包含中轨道准备和路线过渡时间，精灵先行的预览时间另计。时间结束时玩家和精灵停止前进，按 R 重开或 N 切换下一关。计分结果保留在 Session 中，当前不显示结算弹窗。角色只在实际上下跨轨时播放上升或下降动画，抵达任意轨道后恢复平飞。

## 示例配置与资源

两关都在 `BreathDemo.unity` 中，分别是 `Assets/Prefabs/Levels` 下的独立 Prefab 实例：

- `Level_001_FirstFlight`：吸气 3 秒，呼气 4 秒，游戏 45 秒。
- `Level_002_SlowBreath`：吸气 4 秒，呼气 6 秒，游戏 60 秒。
- 两关默认：每秒 10 分，精灵先行 2 秒，中轨道准备 3 秒，引导换轨 0.65 秒，移动速度 4，轨道间距 2.2，玩家换轨 0.16 秒。

在 `Assets/Settings/Levels` 下的 `BreathLevelSettings` 资源中配置名称、吸气时长、呼气时长、总时长、每秒分数、先行时间、准备时间、引导换轨时间、移动速度、轨道间距和玩家换轨时间。

- `Preparation Seconds`：玩家所在位置的路线先在中轨道停留多久；设置为 0 可跳过准备段。
- `Guide Transition Seconds`：引导在轨道之间平滑移动的时长，须大于 0；越大曲线越舒缓。
- `Guide Lead Seconds`：精灵比玩家提前出发多久，与中轨道准备时间独立。
- `Lane Transition Seconds`：玩家按键后换轨动画的时长，与引导移动时间独立。

精灵路线会在每次进入时根据关卡配置重新生成，三条逻辑轨道没有固定线条对象。森林背景按摄像机世界位置自动循环，不受关卡长度限制。角色 GIF 的转换、状态对应和美术配置见 [ArtAssets.md](ArtAssets.md)。

## 实现分工

- `BreathPattern / BreathSession`：独立的时间、路线和计分模型。
- `BreathInput / KeyboardBreathInput`：输入接口和 W/S 键盘实现。
- `ExternalBreathInput`：提供 `Submit(BreathLane)` 接口，供将来的传感器适配器提交状态；当前没有实现任何传感器通信协议。
- `BreathPlayer`：自动前进、换轨视觉和相机跟随目标。
- `BreathLevel`：关卡配置、出生点、精灵与逐渐显现的路径，负责进入准备和退出清理。
- `BreathLevelManager`：唯一切换入口，通过 `SetActive` 管理关卡实例；切换和重开时创建新的计分状态并重置玩家、路径和相机。HUD 为可选依赖。
- `BreathHud`：保留的可选 UI 实现，当前示例场景没有 HUD 实例。

本原型使用 URP 自定义透明 Sprite Shader、Bloom 与 Vignette，PrimeTween 管理换轨动画，Cinemachine 管理水平跟随。uGUI / TextMeshPro 保留供未来界面使用；当前示例只有游戏内容。现有 Odin Inspector 可辅助编辑关卡配置。当前玩法没有资源远程加载、物体选择描边或过场编排需求，Addressables、OutlineFx、Timeline 和 Visual Scripting 保留供后续功能评估。

## 生成与验证工具

`WingSinger/Build Breathing Demo` 用于在没有示例场景时生成资源与场景。工具拒绝覆盖已经存在的示例场景，也拒绝丢弃未保存的场景修改。

`WingSinger/Validate Breathing Demo` 执行确定性逻辑检查。完整的生成与 Play Mode 验证由 `BreathDemoValidation.BuildAndValidateBatch` 执行；验证涉及短时配置副本，不会修改正式配置资源。

完整验证覆盖路线周期、按键冲突、预览不计分、正确停留、换轨过渡、跨周期长帧、结束时长截断、结束后冻结、不同帧率一致性、无效参数、真实 Prefab 切换、摄像机跟随、计时结束结算及重开清理。

本次验证已在隔离项目副本中使用 Unity 2022.3.62f2 完成编译和实际 Play Mode 检查。副本中的玩法与编辑器脚本和交付代码一致；通过记录见 `ValidationReport.txt`，运行截图见 `Images/BreathDemo.png`。

后续增加关卡应继续遵循 `DevelopmentGuidelines.md`，不要将本原型改为逐关加载 Unity 场景。
