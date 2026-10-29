# 基础玩法与示例关卡

## 运行方式

使用 Unity 2022.3.62f2 打开项目，打开 `Assets/Scenes/BreathDemo.unity`，点击 Play。原有 `MainScene.unity` 保留；示例场景作为首个构建场景。

已接入呼吸传感器输入：默认 Windows x64 BLE 自动发现 ESP32_C3_Mini 并订阅 CSV 通知，无握手；最近 5 个 ADC 均值 <1200 为吸气、>1400 为呼气，中间为无输入，中心/偏差可调。保留 USB 115200 调试模式及原阈值。没有设备连接时默认保留以下键盘操作，连接后优先使用传感器。BLE 按用户提供的 sketch_oct27b 协议对接，配置与连接见 [BluetoothInput.md](../Hardware/BluetoothInput.md)，USB 调试见 [ArduinoInput.md](../Hardware/ArduinoInput.md)。

- 按住 W：吸气，前往上轨道。
- 按住 S：呼气，前往下轨道。
- 松开两键或同时按下：回到中轨道，可收集该轨道上的纸制物体。
- R：重开当前关卡。
- N：切换下一个关卡，两关循环。
- 1 / 2：直接进入对应示例关卡。

“按住控制、松开回中间”是本次原型对输入方式的约定。未来可根据传感器输出调整，不影响标准路线与计分模型。

## 核心规则

玩家先在中轨道从画面左侧飞入正常位置，同时纸鹤引导先出发，沿路线显现纸星星收集物。玩家入场期间已经向右接近路线起点，镜头与背景同步滚动；到位后直接保持正常速度前进，预览结束时连续进入正式路线计时，不在原地等待。正交摄像机从正面观察场景，水平跟随玩家；切换轨道时相机不会上下追随。素材对应和离散计分细节见 [PaperCollection.md](PaperCollection.md)。

路线按空间位置确定：先在中轨道行进 `preparationSeconds` 秒，再用 `guideTransitionSeconds` 秒平滑进入上轨道。随后上轨道持续吸气时长，平滑移到下轨道，下轨道持续呼气时长，再平滑返回上轨道，循环往复。引导仍使用缓入缓出的连续曲线，但默认不再显示连续线条。

玩家应收集自身前方的纸制物体，而非直接追随引导当前所处轨道。平直段首尾必有收集物，段内均匀排列；上下换轨曲线经过中轨的交点也有一颗。当前画面只保留背景、角色、纸鹤引导和收集物，没有顶部、底部、轨道文字及结算 UI 和三条固定黑色轨道线。

分数等于收集数量乘每颗分值，不再按路线停留时间加分。实际身体接触收集物才会计分，每颗只计一次；准备段中轨和换轨中经过的中轨收集物也可得分，预览阶段不计分。收集时物体发亮并缩小消失，角色出现短暂闪光。

游戏总时长只计算玩家到达路线起点后的时间，包含中轨道准备和路线过渡时间，精灵先行的预览时间另计。时间结束时玩家和精灵停止前进，按 R 重开或 N 切换下一关。计分结果保留在 Session 中，当前不显示结算弹窗。角色只在实际上下跨轨时播放上升或下降动画，抵达任意轨道后倒放对应的姿态过渡素材，再恢复平飞（默认收姿 0.28 秒）。保持在上、下轨时继续平飞；新方向输入可打断收姿。

## 示例配置与资源

两关都在 `BreathDemo.unity` 中，分别是 `Assets/Prefabs/Levels` 下的独立 Prefab 实例：

- `Level_001_FirstFlight`：吸气 3 秒，呼气 4 秒，游戏 45 秒。
- `Level_002_SlowBreath`：吸气 4 秒，呼气 6 秒，游戏 60 秒。
- 两关默认：每颗 10 分，平直段间隔最多 0.5 秒，引导先行 2 秒，中轨道准备 3 秒，引导换轨 0.65 秒，移动速度 4，轨道间距 2.2，玩家换轨 0.16 秒。

在 `Assets/Settings/Levels` 下的 `BreathLevelSettings` 资源中配置名称、吸气时长、呼气时长、总时长、每颗分值、收集间隔、判定半径、先行时间、准备时间、引导换轨时间、移动速度、轨道间距和玩家换轨时间。

- `Preparation Seconds`：玩家所在位置的路线先在中轨道停留多久；设置为 0 可跳过准备段。
- `Guide Transition Seconds`：引导在轨道之间平滑移动的时长，须大于 0；越大曲线越舒缓。
- `Guide Lead Seconds`：精灵比玩家提前出发多久，与中轨道准备时间独立。实际预览时长至少覆盖玩家入场。
- `Player Entrance Seconds`：从画面左侧飞入的时长，默认 1.25 秒，0 关闭；入场发生在持续向右接近路线的预览期间，到位后无停顿接续正常飞行；不扣正式游戏时间、不计分，预览期间保持中轨。
- `Lane Transition Seconds`：玩家按键后换轨动画的时长，与引导移动时间独立。

精灵路线会在每次进入时根据关卡配置重新生成，三条逻辑轨道没有固定线条对象。森林背景按摄像机世界位置自动循环，不受关卡长度限制。角色 GIF 的转换、状态对应和美术配置见 [ArtAssets.md](../Art/ArtAssets.md)。

## 实现分工

2026-10-25：主场景已接入 13 页 UI，启动停留首页，起飞才激活关卡。原有两关通过关卡定义与目录 SO 接入选关，均可初始选择。参数资源和 Prefab 保持原引用，详见 [UI 交互与关卡目录](../Development/UIInteractionsAndLevelCatalog.md)。下文的旧 HUD 与生成工具说明保留用于原型维护；日常运行使用 BreathDemo。

- `BreathPattern / BreathSession`：独立的时间、路线和计分模型。
- `BreathInput / KeyboardBreathInput`：输入接口和 W/S 键盘实现。
- `ExternalBreathInput`：保留 `Submit(BreathLane)` 通用接口，供其他外部输入适配器提交状态；当前 Arduino 已由独立接收器接入。
- `ArduinoBreathInput / BreathSensorClient / WindowsSerialTransport`：当前 Arduino 115200 串口接收、自动识别重连与三档数值输入，位于公共 GameRoot，不随关卡关闭。
- `BreathPlayer`：自动前进、换轨视觉和相机跟随目标。
- `BreathLevel / PaperCollectible`：引导动画、离散收集物池、显现、接触判定与退出清理。
- `BreathLevelManager`：唯一切换入口，通过 `SetActive` 管理关卡实例；切换和重开时创建新的计分状态并重置玩家、路径和相机。HUD 为可选依赖。
- `BreathHud`：保留的可选 UI 实现，当前示例场景没有 HUD 实例。

本原型使用 URP 自定义透明 Sprite Shader、Bloom 与 Vignette，PrimeTween 管理换轨动画，Cinemachine 管理水平跟随。uGUI / TextMeshPro 保留供未来界面使用；当前示例只有游戏内容。现有 Odin Inspector 可辅助编辑关卡配置。当前玩法没有资源远程加载、物体选择描边或过场编排需求，Addressables、OutlineFx、Timeline 和 Visual Scripting 保留供后续功能评估。

## 生成与验证工具

`WingSinger/Build Breathing Demo` 用于在没有示例场景时生成资源与场景。工具拒绝覆盖已经存在的示例场景，也拒绝丢弃未保存的场景修改。

`WingSinger/Validate Breathing Demo` 执行确定性逻辑检查。完整的生成与 Play Mode 验证由 `BreathDemoValidation.BuildAndValidateBatch` 执行；验证涉及短时配置副本，不会修改正式配置资源。

路线验证覆盖周期、按键冲突、预览、换轨、跨周期长帧、时长截断、结束后冻结和无效参数。当前离散收集版本另外通过 `Tools/CollectionLogicTests` 与 `PaperCollectionSetup.ApplyAndValidateBatch` 验证布点、逐颗计分、收集扫掠、对象池、真实 Prefab 切换和重开。

2026-10-22 的当前版本已使用现有项目缓存，在 Unity 2022.3.62f2 完成编译和 Play Mode 检查，记录见 [PaperCollectionValidation.txt](../Validation/PaperCollectionValidation.txt)，画面见 [PaperCollection.png](../Images/PaperCollection.png)。`ValidationReport.txt` 和 `Images/BreathDemo.png` 保留为早期原型的历史验证记录。

后续增加关卡应继续遵循 `DevelopmentGuidelines.md`，不要将本原型改为逐关加载 Unity 场景。
