# WingSinger 开发指南

本指南适用于整个项目，是后续功能设计、编码、资源制作和验收的依据。核心要求是：合理使用项目已有工具，并在同一个场景中通过关卡 Prefab 实例的激活状态管理关卡。

## 1. 项目基线与当前状态

- 2026-10-29 默认硬件输入改为 Windows x64 BLE，继续复用共享 `ArduinoBreathInput` 与原阈值，USB 可选调试。硬件组尚未确认实际 BLE 协议；软件提供可配置 UUID 和参考固件，连接及验证范围见 `Docs/Hardware/BluetoothInput.md`。暂停时 BLE 接收继续，未知电量保持 `--`。

- Unity：2022.3.62f2，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 渲染管线：URP 14.0.12，以 `Packages/manifest.json` 为准。
- 当前可运行的玩法入口与首个构建场景为 `Assets/Scenes/BreathDemo.unity`；`Assets/Scenes/MainScene.unity` 保留在构建列表中。
- 画质配置：Performant、Balanced、High Fidelity；项目保存的当前档位和桌面默认档位为 High Fidelity。
- 主场景已经配置 Global Volume 和相机后处理；Volume 包含 Bloom、Vignette、Tonemapping。
- Balanced、High Fidelity 的 Renderer 已配置 SSAO；OutlineFx 已导入，但尚未接入这三个正式 Renderer。
- 基础玩法在 `Assets/Scenes/BreathDemo.unity` 中实现，包含统一关卡管理器与两个关卡 Prefab 实例。运行方式和模块职责见 `Docs/Design/BasicGameplay.md`；原有 `MainScene.unity` 保留。
- 2026-10-25 已整理第一方代码与文档目录，脚本 GUID、现有程序集和关卡实现保持不变，见 `Docs/Development/ProjectStructure.md`。
- 13 个 UI 页面位于 `Assets/Prefabs/UI/Screens`，预览场景为 `Assets/Scenes/UI/UIFlowPreview.unity`（不加入游戏构建列表）。页面关系与接入差异见 `Docs/Design/UI/UIFlowDesign.md`，正式关卡继续遵守下述单场景管理约定。
- `BreathDemo` 已通过 `BreathUIFlowController` 接入完整 UI 导航；启动停留首页，起飞后才激活关卡。暂停冻结游戏会话与缩放时间，USB 接收继续运行；菜单退出由统一管理器清理关卡。
- 选关数据使用 `Assets/Settings/Levels/WingSingerLevelCatalog.asset` 和对应 `BreathLevelDefinition` SO，关联原有 Prefab 及 `BreathLevelSettings`。现有两关均可初始选择；按钮交互使用 PrimeTween，详见 `Docs/Development/UIInteractionsAndLevelCatalog.md`。
- UI Canvas 使用 Scale With Screen Size、1920×1080 参考分辨率；参考稿坐标由 `UIPageViewport` 等比适配当前窗口和 Safe Area。调整界面后检查实际控件及文字边界，不能只检查参考分辨率；详见 `Docs/Development/UIViewportLayout.md`。
- 2026-10-26 按方案 A 合入主分支的双层视差背景：共享远景 0.65 倍、透明前景 1.15 倍，仍保留现有 UI、关卡 SO 和单场景管理。配置与验证见 `Docs/Art/ParallaxBackground.md`。

## 2. 必须遵守的关卡管理方式

### 同一场景、多个关卡 Prefab 实例

游戏运行时保留同一个主场景。每个关卡制作成独立 Prefab，在主场景中持有关卡实例，通过实例根对象的 `GameObject.SetActive(true/false)` 切换关卡。

- 默认在主场景预先放置关卡实例，启动时统一确定初始关卡，其余实例保持关闭。
- 游戏进行时恰好有一个关卡根对象处于激活状态；启动、菜单或切换过渡期间允许没有激活关卡。
- 关卡必须放在不同的独立根对象下，不嵌套在另一关卡根对象中。
- 关卡 Prefab 是资源模板；实际切换的是场景中的 Prefab 实例，不是直接修改资源模板。
- 普通切关不使用 `SceneManager.LoadScene` 或 Additive 场景加载，也不通过每次销毁和重新实例化关卡来实现重置。
- 后续若确有内存需求，可考虑首次按需加载并保留关卡实例，但后续切关仍须遵守激活状态管理约定。改变这一核心架构前应先与项目负责人确认并更新文档。

建议的主场景组织方式（名称为建议，不代表现有对象）：

```text
MainScene
├─ GameRoot            # 全局游戏状态、关卡管理器
├─ SharedSystems       # 公共摄像机、音频、UI、公共灯光与 Volume
├─ Player              # 需要跨关保留时放在公共区域
└─ LevelRoot
   ├─ Level_001        # 关卡 Prefab 实例
   ├─ Level_002        # 关卡 Prefab 实例
   └─ Level_003        # 关卡 Prefab 实例
```

### 公共对象与关卡内容的边界

- 关卡 Prefab 包含本关的环境、碰撞体、敌人、机关、交互物、出生点和本关配置。
- 关卡产生的临时对象应挂在本关根对象或其运行时子节点下，退出时统一清理；放入全局对象池的对象必须显式归还。
- 统一管理器、公共 UI、主摄像机和跨关状态放在关卡根对象之外，防止关闭关卡时一起失效。
- 默认复用公共玩家，并在进入关卡时设置位置、朝向和需要重置的运动状态。若玩法需要关卡自带玩家，应明确归属，避免两套玩家同时运行。
- 公共摄像机、AudioListener 和 EventSystem 避免在每个关卡重复配置。
- 关卡可以提供灯光和局部 Volume，但需要明确它们与公共设置的关系、优先级和退出行为。
- 关卡不直接引用另一关卡内部对象，通过公共服务或管理器通信。

### 统一切换与生命周期

由独立的关卡管理器维护关卡标识、实例引用和当前关卡，并作为进入、下一关、上一关和重开的统一入口。关卡自身可以发出完成或失败请求，由管理器执行切换。

普通切关流程：

1. 验证目标标识和实例；目标无效时保留当前关卡并报告可理解的错误。
2. 锁定切换入口，暂停玩家输入，防止重复请求产生竞争。
3. 执行当前关卡退出流程，清理运行状态，然后关闭当前关卡根对象。
4. 准备并重置目标关卡数据；激活目标关卡根对象，完成需要激活对象参与的进入流程。
5. 设置玩家出生状态、摄像机目标、UI 和音频，确认初始化完成后再允许玩法运行。
6. 更新当前关卡记录并恢复输入。异常时释放切换锁，进入明确的恢复或错误状态，不留下两个激活关卡或与实际激活状态不一致的记录。

关卡应有明确的初始化、进入、退出和重置职责，接口与类名在实际实现时决定。不要把全部逻辑塞进 `OnEnable` 或 `OnDisable`：管理器负责切换顺序，关卡负责本关状态。

- `Awake`、`Start` 不会在每次重新激活时重跑，不能用它们代替重开逻辑。
- 首次激活与再次激活都必须正确；不要假定关闭对象上的 `Awake` 已经执行。
- `SetActive(true)` 会触发启用回调。依赖和数据的准备顺序必须明确，避免 `OnEnable` 提前开始玩法。
- `SetActive(false)` 不会自动恢复生命值、机关状态、Transform 或全局服务中的任务。
- 重开当前关卡也走退出、重置、重新进入流程，即使目标标识与当前标识相同。
- 重置敌人、机关、计时器、关卡内分数、临时对象和玩家运动状态；跨关进度与关卡临时状态分别管理。
- 退出时取消事件订阅、补间动画、音效和异步任务，并清理公共系统中持有的本关引用。
- 异步回调检查关卡是否仍有效，可通过取消标记或切换序号防止上一关的结果写入下一关。不要只依赖关闭对象来取消任务。

## 3. 已有工具的使用约定

按功能评估下列工具。适合时优先使用现有能力；不要求每个功能都使用插件，也不要求立即启用全部工具。

- **URP**：作为统一渲染基础。使用兼容 URP 的材质和 Shader；导入资源时检查材质、光照与透明效果。画质配置在 `Assets/Settings` 中管理，不另行引入渲染管线。
- **SSAO、Volume 与后处理**：SSAO 用于接触与凹陷处的遮蔽；Bloom 用于高亮光晕；Vignette 用于边缘暗化；Tonemapping 用于明暗转换。按视觉需求调整，并核对目标画质档位。SSAO 是 Renderer Feature，不由 Volume 控制。
- **OutlineFx 1.1.0**：优先用于选中、悬停或可交互对象的轮廓提示。使用前为所需档位的 Renderer 配置 `OutlineFxFeature`，并给目标对象设置描边组件。关卡退出时清除公共系统中的选中引用。
- **PrimeTween**：优先评估用于 UI 出入场、缩放、淡入淡出和简单位置动画。管理动画句柄或所属目标，在退出或重置时停止动画并恢复初始值，避免旧动画影响重新进入的关卡。
- **Odin Inspector 4.0.2.4**：用于整理配置、条件显示字段、调试按钮和编辑器辅助。运行时代码与编辑器代码分开；确有数据需求时再使用 Odin 序列化，不把 Inspector 显示功能当成运行时逻辑。
- **Cinemachine 2.10.7**：优先评估用于跟随、构图和镜头过渡。切关时更新目标与边界，重开时处理出生点跳转造成的镜头拖尾。公共主摄像机不随关卡关闭。
- **Addressables 1.22.3**：用于有实际需求的异步资源加载、分组和资源生命周期管理。默认预置的关卡无需强行改为 Addressables。加载与释放责任必须明确；关闭对象不等于释放内存，不释放仍被关卡或公共对象使用的句柄。
- **uGUI / TextMeshPro**：用于菜单、HUD、按钮和文字；公共 UI 放在关卡根对象之外。切关后刷新显示和输入状态，避免重复注册按钮事件。
- **Timeline 1.7.7**：用于需要时间编排的过场、镜头、动画和音频。关卡退出时停止相关 Director，清理公共对象绑定或状态；简单按钮动画优先评估 PrimeTween。
- **Visual Scripting 1.9.4**：可用于适合节点编排的局部逻辑。统一关卡管理入口应保持清晰，避免节点图和 C# 分别维护不同的当前关卡状态。
- **IDE 集成、Test Framework 与版本控制工具**：用于开发、验证和协作。测试针对实际风险；不要修改插件示例场景来代替正式关卡，也不在第三方插件源码中放置游戏逻辑。

不同画质档位可能使用不同 Renderer。新增渲染功能时，应明确哪些档位需要启用，检查公共 Volume 与关卡 Volume 的叠加结果，并验证关卡关闭后不残留画面效果。

## 4. 新增关卡与验证

建议正式关卡资源放在 `Assets/Prefabs/Levels`，管理代码放在 `Assets/Scripts/Levels`。这些路径用于后续组织，目前不要求创建空资源或占位实现。

新增关卡时：制作独立 Prefab，定义出生点和关卡配置，在主场景登记实例与唯一标识，保持非初始关卡关闭，并补齐退出与重置行为。

与关卡系统有关的修改至少验证：

- 首次启动时只有初始关卡激活，玩家、UI 和摄像机状态正确。
- 从 A 切到 B 后，A 的碰撞、敌人、音效、动画和回调不再影响当前玩法。
- B 返回 A、重开 A 时，A 恢复预期初始状态，跨关进度按设计保留。
- 连续快速切换、重复请求或无效关卡标识不会产生多个激活关卡或卡死输入。
- 关闭期间异步任务完成时，不产生过期状态写入或无效引用。
- 使用到的渲染功能在目标画质档位正常，没有粉色材质、重复相机或 AudioListener。

每次交付说明修改内容、验证方式和实际限制。后续实现如果改变本文约定，应同步更新文档和根目录 `AGENTS.md`。

## 5. 增量编译与日常迭代

### 基本原则

后续开发默认采用增量编译，减少不必要的编译次数和受影响范围，保留必要的编译与运行验证。

- Unity 的 C# 增量编译通常以程序集及其依赖关系为单位，不是只重编译一个 `.cs` 文件。当前游戏脚本属于 `Assets/Scripts/Main.asmdef` 定义的 `Main` 程序集；修改其中一个脚本时，整个相关程序集可能重新编译。
- 资源刷新、C# 编译、域重载和 Player 构建是不同过程。域重载或长时间等待不直接等于全项目重新编译；判断时查看实际编译的程序集和日志。
- `CompilationPipeline.RequestScriptCompilation()` 的普通请求不等于清缓存全量编译，但重复请求仍会造成额外等待。`RequestScriptCompilationOptions.CleanBuildCache` 才是明确清理编译缓存并重编译所有脚本的选项，日常迭代不使用。
- 修改公共类可能扩大受影响的程序集范围，但不必然导致所有程序集重编译。接口和事件用于合理解耦，不保证消除程序集层面的依赖。

### 默认工作流程

1. 修改前确定本次变化属于代码、资源、配置还是文档。关卡参数、Prefab 属性、材质和 UI 配置优先通过已有配置资源或 Unity MCP 修改，不为改数值而重写脚本。
2. 将同一功能的相关代码修改成批完成并保存，不在每改一个文件后主动请求编译；不通过重写相同内容或触碰无关文件的时间戳来触发刷新。
3. 复用已经打开的 Unity 和已连接的 MCP，先等待正常资源导入、增量编译和域重载结束。通过 `mcpforunity://editor/state` 检查忙碌状态，再通过 `read_console` 检查编译错误。编辑器就绪本身不证明外部文件已经导入，必要时结合变更文件的导入记录和编译日志确认新代码生效。
4. 若外部修改未被导入，优先定向导入实际变化的文件；需要脚本化导入时使用 `AssetDatabase.ImportAsset` 的普通导入选项，避免默认加 `ForceUpdate`。同一批导入完成后再检查一次结果。
5. 只有确认变更已导入、编辑器空闲且仍未编译时，才请求一次普通脚本编译。MCP 可使用 `refresh_unity(scope="scripts", compile="request", wait_for_ready=true)`；该调用不是独立的“增量编译开关”，也不替代外部文件的资源导入。
6. 编译通过后完成与改动相称的验证。代码未再改变、检查已通过时，不重复刷新和编译。纯文档修改不请求 Unity 编译；纯资源修改按需导入和验证，不主动请求 C# 编译。

### Unity MCP 的刷新约束

- 不默认调用 `refresh_unity(mode="force", scope="all", compile="request")`。
- 已检查的 MCP for Unity 10.3.0 实现中，`mode="if_dirty"` 与 `mode="force"` 都可能在资源刷新时走强制刷新路径；不能仅把参数改为 `if_dirty` 就视为低成本增量操作。
- `scope="scripts"` 避免该工具的全资源刷新分支，但仍可能发起一次编译流水线和域重载，因此只用于上述必要情况。
- 范围更大的刷新用于确有资源发现、依赖或导入问题时；先说明原因，不将其作为每次开发完成的固定步骤。不修改第三方 MCP 插件源码实现这些约定。

### 缓存、程序集与依赖

- 日常开发不删除 `Library`、`Library/Bee`、`Library/ScriptAssemblies` 等缓存，不执行 Reimport All 或清缓存构建来替代排查。
- 验证优先在当前项目与当前编辑器内完成，使用现有测试与 MCP。确需隔离时先说明原因并尽可能复用安全、可用的验证环境；不把每次复制项目、从零生成 Library 作为默认验证流程。
- 不为普通玩法迭代频繁切换构建平台、Scripting Define Symbols、脚本后端、Unity 版本或包版本；集中安排必需的依赖更新。
- 保持 `.asmdef`、程序集名称、目录归属与引用关系稳定。当前代码规模下不为了本约定立即重拆程序集，避免引入一次无必要的结构变更。
- 后续模块增多、编译日志显示依赖范围过大时，再按实际边界拆分核心逻辑、表现和 Editor/测试程序集。保持依赖单向、引用精简，稳定接口放在低变动模块；移动脚本时保留 `.meta` 和资源引用。

### 需要扩大编译范围时

编译缓存确已损坏、引擎或包升级、平台或宏定义发生必要变化时，可以进行较大范围的重编译。先记录当前症状，检查错误、依赖与导入状态，尝试最小范围修复；确需清缓存或全量验证时说明证据与具体范围，避免因耗时较长就直接认定缓存损坏。

本约定不通过关闭自动刷新、跳过编译错误或关闭域重载来掩盖问题。域重载设置会影响静态状态和事件订阅，不能当作增量编译开关随意更改。

参考：

- [用户提供的 CSDN 问答：Unity 增量编译常见问题](https://ask.csdn.net/questions/8617960)。采用其中减少耦合、稳定配置和合理划分程序集的建议；具体机制以 Unity 2022.3 文档和当前插件源码为准，不直接采用其示例监控代码或未经核实的热重载建议。
- [Unity 2022.3：程序集定义](https://docs.unity3d.com/2022.3/Documentation/Manual/ScriptCompilationAssemblyDefinitionFiles.html)
- [Unity 2022.3：RequestScriptCompilation](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Compilation.CompilationPipeline.RequestScriptCompilation.html)
- [Unity 2022.3：CleanBuildCache](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Compilation.RequestScriptCompilationOptions.CleanBuildCache.html)
