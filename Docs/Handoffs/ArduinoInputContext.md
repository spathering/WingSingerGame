# Arduino 呼吸输入：Agent 接手上下文

2026-10-29 更新：当前游戏默认改为 BLE，旧串口快照仍只用于 USB 调试。新接收器、参考固件、待硬件确认的 UUID/格式与连接步骤见 [BluetoothInput.md](../Hardware/BluetoothInput.md)。不要从历史 ZIP 恢复并覆盖当前实现。

整理日期：2026-10-22（Asia/Shanghai）。项目：`H:\PolyU\PM\WingSingerGame`。本次整理时最近提交为 `6a35e0d arduino映射`，工作区原先干净。

本文件可直接作为其他 agent 的任务背景。实际要继续做的工作，以用户的新要求为准；目前输入映射已经实现，不需要从零搭建。配套 ZIP 是上下文、相关源码和测试的快照，不是完整 Unity 项目或可导入的 `.unitypackage`。继续修改时，以实际仓库内的最新文件为准。

2026-10-26 合并说明：配套 [ArduinoInputContext.zip](ArduinoInputContext.zip) 保留自上游的历史快照，内部旧目录及硬件结论仅供追溯；当前协议、可调阈值、调试和固件入口以 [ArduinoInput](../Hardware/ArduinoInput.md) 与实际代码为准，不从快照覆盖当前工程。

## 1. 用户已确认的要求

- Arduino 通过 USB 串口输出传感器数值，固定 **115200** 波特率。
- **数值 ≤400：吸气，映射上轨，等效 W。**
- **数值 ≥1000：呼气，映射下轨，等效 S。**
- **400 < 数值 < 1000：无输入，映射中轨。**
- 边界包含关系已明确：400 属于吸气，1000 属于呼气。
- 用户希望自动检测串口号，不固定 COM3 或 COM4。
- 用户已确认硬件按以下协议处理：每行一个数值；收到 `WINGSINGER_HELLO` 后回复 `WINGSINGER_BREATH_V1`。

当前实现选择：8N1、无流控；无设备时保留键盘后备；超时和重连采用下列默认参数。不要把这些实现默认值混同为用户要求永不可改。默认没有额外滤波、平均或滞回，避免悄悄改变上述映射区间。

## 2. 通信协议

线上的 `\n` 表示一个实际 LF 换行字节，`\r\n` 表示 CRLF；不要发送反斜杠和字母 n 两个字符。

```text
Unity → Arduino: WINGSINGER_HELLO\n
Arduino → Unity: WINGSINGER_BREATH_V1\n
Arduino → Unity: 350\n
Arduino → Unity: 700\n
Arduino → Unity: 1200\n
```

- 接收器必须先验证完整设备标识，之后才接受数值；只有纯数字的其他串口不会被认作传感器。
- 握手和数值允许 LF 或 CRLF；使用 ASCII 文本、InvariantCulture 小数点。
- 数值使用有限 `double` 解析；不接受字段标签、CSV、JSON、NaN 或 Infinity。数值行应独立输出。
- 支持串口拆包、合包；每行最长 64 字符，损坏或超长行整行丢弃，下一换行后恢复。
- 固件应持续发送，例如每 20ms 一行。仅在数值变化时发送会触发 Unity 的数据超时。

## 3. 已实现的场景接线与行为

主玩法场景：`Assets/Scenes/BreathDemo.unity`。

- 共享 `GameRoot` 同时挂载 `BreathLevelManager`、`ArduinoBreathInput` 和原有 `KeyboardBreathInput`。
- `BreathLevelManager.input` 已指向 `ArduinoBreathInput`，不是旧键盘组件。
- `ArduinoBreathInput.keyboardFallback` 引用原有键盘组件。
- 默认 `portOverride=""`：自动枚举非蓝牙 COM 设备，按握手识别。填入指定 COM 号可限制检测范围，但仍要求握手。
- 自动扫描通过 Windows `QueryDosDevice` 获取候选，排除设备映射中包含 Bth 的蓝牙虚拟串口，避免等待远端蓝牙连接；优先尝试上次成功的串口。
- 当前串口实现面向 Windows 编辑器 / Windows 桌面目标。其他平台返回不支持，仍可使用键盘后备。
- 有新鲜传感器数据时，传感器优先；设备已连接但数据过期时返回中轨，不使用键盘覆盖。
- 设备断开或尚未识别时，若 `keyboardWhenDisconnected=true`，允许 W/S 后备。需要完全由传感器控制时，将其设为 false。
- 开场预览阶段，管理器仍保持中轨；正式运行阶段才读取呼吸输入。刚进入 Play 就测试呼吸时，要留意这个预览阶段。
- 切关通过原有关卡 Prefab 根对象 SetActive 完成；接收器在共享区内，重开 / 切关不重建串口连接。
- 组件关闭、退出 Play 或退出程序时停止接收并释放句柄。重新启用创建新接收器，不复用旧读数。
- 没有新增游戏 HUD。运行时从 GameRoot 的组件 Inspector 查看 Connection Status、Connected Port、Latest Value、Fresh Sample。

## 4. 默认参数

`Assets/Scripts/Input/ArduinoBreathInput.cs` 与场景保存值一致：

```text
portOverride               = ""       # 自动识别
inhaleMaximum              = 400
exhaleMinimum              = 1000
sampleTimeoutSeconds       = 0.5      # 无有效新数值时回中轨
handshakeTimeoutSeconds    = 4
reconnectDelaySeconds      = 2
inactivityTimeoutSeconds   = 2        # 无有效数值时关闭连接、重试
keyboardWhenDisconnected   = true
```

波特率常量：`BreathSensorProtocol.BaudRate = 115200`。识别请求每 0.5 秒重发，允许打开串口后 Arduino 重启、启动。连接参数在创建接收器时取值，运行中修改后要关闭、重新启用组件；映射阈值由主线程每次读取。

## 5. 代码入口与职责

路径均相对项目根目录：

- `Assets/Scripts/Input/BreathSensorProtocol.cs`：波特率 / 握手常量、`TryParseSample`、`Resolve`，以及 `BreathSensorLineDecoder`。
- `Assets/Scripts/Input/BreathSensorClient.cs`：后台扫描、握手、只保留最新数值、超时、重连和关闭；定义 `IBreathSerialTransport`、连接配置及只读快照。工作线程不访问 Unity API。
- `Assets/Scripts/Input/WindowsSerialTransport.cs`：Win32 串口枚举、独占打开、115200/8N1 设置、限时读写、取消及句柄释放。没有使用额外串口 DLL，也没有为接串口切换 .NET 配置。
- `Assets/Scripts/Input/ArduinoBreathInput.cs`：继承 `BreathInput`，主线程映射到 `BreathLane`；公开 `Receiver`、`ReceiverSnapshot`、`Connect(...)`、`Disconnect()`。`Connect` 可接收模拟客户端用于测试。
- `Assets/Scripts/Input/BreathInput.cs`：输入统一接口 `ReadLane()`。
- `Assets/Scripts/Input/KeyboardBreathInput.cs`：W/S 后备输入。
- `Assets/Scripts/Input/ExternalBreathInput.cs`：保留通用 `Submit(BreathLane)` 接口，当前 Arduino 接线直接使用 `ArduinoBreathInput`，没有经过此组件。
- `Assets/Scripts/Gameplay/Core/BreathSession.cs`：定义 `BreathLane`，Upper=1、Middle=0、Lower=-1；保留现有玩法会话模型。
- `Assets/Scripts/Levels/BreathLevelManager.cs`：实际读取输入与切关。不要为传感器另建一套关卡或计分管理器。
- `Assets/Editor/Setup/ArduinoInputSetup.cs`：菜单 `WingSinger > Connect Arduino Input`，在已保存的 BreathDemo 中绑定已有输入；拒绝覆盖未保存的场景。
- `Assets/Editor/Setup/BreathDemoBuilder.cs`：新建示例时也调用上述接线工具；原有生成器不会覆盖已存在的示例场景。
- `Assets/Editor/Validation/ArduinoInputValidation.cs`：菜单 `WingSinger > Validate Arduino Input`，用模拟串口驱动真实场景。
- `Tools/SensorInputTests/`：不依赖 Unity 编辑器的 .NET 8 控制台测试，包含可替换的模拟串口。

详细使用说明：[ArduinoInput.md](../Hardware/ArduinoInput.md)。

## 6. 验证证据与限制

上一次实现阶段，已通过：

- **57 项协议 / 接收线程测试**：阈值边界、非法数值、拆包与合包、损坏行、超长行、握手、错误设备、115200、突发数据取最新值、数据超时、重连、忙串口重试、停止清理及 Windows 候选枚举。
- **19 项真实场景模拟串口 Play Mode 检查**：400 驱动上轨、1000 驱动下轨、700 回中轨，重开与 Prefab 切关保持同一连接，过期 / 断线回中轨，关闭和重新启用后旧线程停止，替换 COM 号可继续使用，无新增 UI。
- Unity 增量编译通过；原有 .NET Standard 配置、URP 和包依赖未改变。

报告：[ArduinoInputValidation.txt](../Validation/ArduinoInputValidation.txt)。这些是已保存的历史结果；本次仅打包上下文，没有重新编译或重跑。

**真实 Arduino 端到端联调尚未完成。** 上次检查本机只有 COM3 / COM4 两个蓝牙串口，没有 Arduino；自动筛选后候选为空。设备连接情况会变化，接手时重新枚举。不要把历史串口号当成当前传感器，也不要声称已通过实机测试或 Windows Player 构建测试。

## 7. 复现与后续联调

在项目根目录、Windows 与 .NET 8 SDK 环境运行：

```powershell
dotnet run --project Tools/SensorInputTests/SensorInputTests.csproj -c Release
```

Unity 运行检查：打开已保存的 BreathDemo，执行 `WingSinger > Validate Arduino Input`。该菜单会进入 / 退出 Play；报告输出至 `Logs/ArduinoInputValidation.txt`。模拟客户端不需要真实硬件。验证期间临时允许后台运行，结束后恢复，不修改 Player Settings。

用户后续要求实机联调时，依次确认：

1. Arduino 使用数据线连接，驱动和 COM 设备可见。
2. 关闭占用同一串口的 Arduino Serial Monitor / Plotter 等程序。
3. 固件按原协议回应握手，且持续发送每行一个有限数值。
4. Play 后识别为 Connected，数值与硬件读数一致；确认 400、1000 两个边界与中间区间。
5. 验证吸气 / 呼气 / 无输入分别驱动上 / 下 / 中轨；等待开场预览结束后检查操作。
6. 断流、拔插、更换 USB 口、重开、切关及停止 Play 后端口释放均正常。
7. 记录实际板型、COM 号、输出样例、测试结果，再更新联调结论。用户尚未提供 Arduino 板型、传感器型号、原始固件和实测数据日志。

## 8. Unity MCP 与开发约束

- 修改功能前读取仓库 `AGENTS.md`、`Docs/DevelopmentGuidelines.md`。
- 使用 Unity **2022.3.62f2**、现有 URP **14.0.12**；关卡在同一场景内切换 Prefab 根对象激活状态，正常游玩时恰好一个激活。
- 保留现有用户修改，不改第三方源码；本输入模块未改玩法动画、计分和渲染配置。
- 开始编辑器操作前先确认 MCP 所连项目是 WingSingerGame，读取实例、项目根目录、场景和编译状态。
- 上次可用 MCP 地址是 `http://127.0.0.1:8081/mcp`，健康检查为 `/health`。8080 曾被 Steam 占用；`Docs/Development/UnityMcpSetup.md` 中的 8080 是更早记录，接手时以实际状态为准，不为腾端口停止无关程序。
- 当时当前聊天未暴露 Unity 原生工具入口，通过本地 MCP HTTP 客户端完成了项目读取、定向导入、菜单执行及控制台检查。这不保证其他 agent 已经自动加载工具，也不代表本机 Codex 的全局 MCP 配置已经同步改变。
- 当前工作目录的 `Temp/unity_mcp.py` 曾作为临时客户端；Temp 被 Git 忽略，不应当作必需依赖，本压缩包不包含它。优先复用接手环境中可用的 Unity MCP 工具。
- 成批保存修改，复用已开编辑器和 Library 缓存；必要时定向普通导入变更脚本，再等待相关程序集增量编译。仅导入后未编译才请求一次普通编译。
- 不默认 force/all 刷新，不删 Library / Bee，不 Reimport All，不改变平台、宏、程序集边界或 .NET 配置来完成普通输入迭代。纯上下文文档更新不触发 Unity 编译。

## 9. 可直接复制给接手 agent 的开场提示

> 请先阅读 `Docs/Handoffs/ArduinoInputContext.md`、`AGENTS.md` 与 `Docs/DevelopmentGuidelines.md`，再根据用户本轮具体要求继续工作。Arduino 输入已实现并绑定 BreathDemo：115200 波特率；先进行 WINGSINGER_HELLO / WINGSINGER_BREATH_V1 握手，再接收每行一个数值；≤400 上轨，≥1000 下轨，中间为中轨。保留共享接收器、自动重连和无设备时的 W/S 后备，不要重新搭一套输入系统。历史软件测试通过，真实硬件联调待完成；先核实当前设备、仓库与 MCP 状态，不要将历史结果表述为刚完成的实机验证。
