# ESP32-C3 BLE 呼吸输入

配置入口、独立资源和游戏内面板见 [HardwareSettings.md](HardwareSettings.md)。

## 当前对接协议（2026-10-29）

依据用户提供的 `D:\Studies\ProjMgrt\wingsinger\sketch_oct27b\sketch_oct27b` 内 `sketch_oct27b.ino` 与 `deepseek_html_20261029_3ff63a.html` 对接。工程默认采用这份硬件代码的 **Notify-only CSV**，无需修改或重新烧录硬件。代码阅读与模拟测试不代表真实蓝牙已经连通。

| 参数 | 硬件实现 / Unity 默认 |
| --- | --- |
| 广播名称 | `ESP32_C3_Mini`，大小写精确匹配，可选地址过滤 |
| Service UUID | `6e400001-b5a3-f393-e0a9-e50e24dcca9e` |
| TX Notify UUID | `6e400003-b5a3-f393-e0a9-e50e24dcca9e` |
| RX Write / 握手 | 没有 Write 特征；不发送 HELLO，不等待 IDENTITY |
| 数据格式 | 每个通知一条 ASCII `adcMv,deltaKPa,state`，没有换行符 |
| 示例 | `1234,0.150,EXHALE`；状态为 `REST` / `INHALE` / `EXHALE` |
| 发送频率 | 约每 100 ms 一个通知；板端每 10 ms 采样 |
| 硬件 | ESP32-C3 GPIO0、两个 10k 电阻分压；ADC 字段为原始 ADC 毫伏 |

设备通过广播名（可附加地址）和服务、通知 UUID 筛选，收到首条完整、有效的 CSV 后才进入接收状态。没有原来的身份握手；不能只按通用 UART UUID 无限制连接任意外设。

## 映射与可调参数

配套网页按最近 **5 个有效 ADC 通知的平均值**判断动作，启动不足 5 个时按现有数量取平均。工程采用同一算法，初始中心基准 **1300 mV**、偏差 **100 mV**：

- 均值 **< 1200 mV**：吸气 / 上轨。
- 均值 **> 1400 mV**：呼气 / 下轨。
- **1200 ≤ 均值 ≤ 1400 mV**：静息 / 中轨，边界与网页一致。

默认值在 `Assets/Settings/Input/WingSingerHardwareSettings.asset` 中配置；游戏内通过 `Settings > Hardware settings` 修改中心、偏差和滤波数量并点击 `Apply & save`。滤波默认 5，变更后重连生效。无效或非有限的基准/偏差保持中轨，并阻止依赖传感器的起飞；修正后即可恢复。游戏内参数保存到本机独立 JSON，不修改工程默认资源。

固件的 `deltaKPa` 是板端平滑传感器电压相对开机基线的估算压力；固件状态使用 ±0.08 kPa 进入、±0.03 kPa 退出的滞回。网页忽略这套状态来控制动作，工程也采用 ADC 均值映射。压差和固件状态仅用于诊断，可能与 Unity 当前状态不同；不能把该估算值当成已校准压力。

在硬件页开启 `Console debug` 并应用，或在默认配置资源中开启 `开启 Console 调试` 后，输出原始 ADC、均值、压差、固件状态、Unity 吸呼气/轨道状态和有效阈值。只输出新样本或状态变化，不按渲染帧重复累计滤波或输出旧数据。相同数值的新通知仍计为新样本。

## 连接步骤

1. 使用已有 `sketch_oct27b` 固件，给板子供电；开机校准时保持两气嘴无压差。
2. 打开电脑蓝牙，关闭网页上位机或其他占用该设备的软件。原生 Windows x64 BLE 不需要虚拟 COM；115200 仅用于固件上传/USB 日志。
3. 打开 `Assets/Scenes/BreathDemo.unity`。共享 `GameRoot / ArduinoBreathInput` 引用独立配置资源，默认使用 `Bluetooth LE`、`Csv Notifications`、设备名 `ESP32_C3_Mini` 和上述两个 UUID。`Bluetooth Write Uuid` 在 CSV 模式下被忽略。
4. Play，或在控制器页面点检查连接。有效通知到达后显示 `BLE connected`；Inspector 可查看原始值、均值、压差与固件状态。电量继续显示 `--`。
5. 打开 Console 调试，实测静息/吸气/呼气，结合静息值调整中心和偏差。默认 1300/100 来自网页初值，并非该设备的自动校准结果。
6. 检查关闭板子后中轨与重连，再检查暂停、重开、切关及退出 Play。无需为切关重新创建接收器。

可用 `WingSinger > Connect Bluetooth Input` 对已保存的 BreathDemo 应用当前硬件连接参数；它接入已有独立配置资源，保留其中的参数。连接模式、设备名、UUID、滤波数量变更后需重新连接。

## 数据边界、超时与生命周期

- 原生 DLL 保留每个 Notify 的边界，CSV 模式一次 Read 读取一个完整通知；不等待换行，也不拼接相邻样本。严格要求三个字段、有限数值、ADC 在 0..4095 范围、已知英文状态。
- 畸形、截断、未知状态、非 ASCII、过长通知整条丢弃，不刷新新鲜度，不污染均值。该硬件协议没有跨通知分片标记；不猜测或拼接截断包。若实际固件后续分片，需修改协议而不是拼接猜值。
- 首个有效通知前最多等待 30 秒；原生扫描/GATT 单步骤最多约 8 秒。新样本超过 0.5 秒回中轨，连续 2 秒无有效样本后断开重连，重试间隔 2 秒。无效数据同样不能维持在线输入。
- 每次重连清空滤波窗口，不复用上一连接样本；已连接但陈旧时不让键盘覆盖。断开后可用 W/S 后备，取决于原有 Inspector 开关。
- 暂停时后台接收继续。切关、重开复用共享接收器与场景关卡实例；退出 Play/关闭输入组件会停止扫描、撤销通知并释放连接。
- 当前原生实现仅支持 Windows x64；手机、macOS、Linux、Windows ARM64 尚未实现。

## 旧协议兼容

USB 保留 [ArduinoInput.md](ArduinoInput.md) 中的 115200、HELLO/IDENTITY、换行纯数字与可调 ≤400/≥1000 阈值，不做 CSV 滤波。

仓库 `Firmware/WingSingerBreathSensorBLE` 是**另一套旧参考固件**：广播 `WingSinger Breath`，有 RX Write 特征 `6e400002-b5a3-f393-e0a9-e50e24dcca9e`，需要握手并发送换行数字。使用它时手动选 `Handshake Uart` 并填写该设备名；它仍使用原可调 ADC 阈值。它不等同于用户提供的 `sketch_oct27b`，不要为匹配旧参考固件覆盖当前硬件。

## 验证

- `dotnet run --project Tools/SensorInputTests/SensorInputTests.csproj -c Release`：USB/旧 UART 回归、硬件 CSV 严格解析、5 点均值、边界、调参、调试、陈旧数据、断线重连，以及实际原生 DLL 的扫描/取消和 Notify-only 接口加载。
- `WingSinger > Validate Bluetooth Input`：实际 Unity Mono 加载 DLL，再使用模拟 CSV 通知验证正式场景中的三态、起飞、暂停、重开、切关、退出及重连。报告见 `Docs/Validation/BluetoothInputValidation.json`。
- `Tools/BluetoothNative/Build.ps1` 重建 Windows x64 DLL；兼容本机旧 Windows SDK 的 C++/WinRT 头文件，不修改外部 SDK。完整构建输出在 `Logs/BluetoothNativeBuild-latest.txt`。
- 实际设备广播/GATT/通知、传感器三态和断电重连仍需实机确认。没有自动烧录，也没有把模拟通知检查表述为实机成功。
- 先前全 UI 导航回归存在按钮动画精确恢复断言失败，本次只验证输入相关流程，不把旧 UI 问题声称为已修复。
