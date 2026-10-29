# ESP32-C3 BLE 呼吸输入

## 当前完成状态

游戏默认从 USB 切换到 **Windows 10/11 x64 BLE**。仍使用主场景 `BreathDemo` 的共享 `GameRoot / ArduinoBreathInput`，不改玩家、阈值或同场景关卡 Prefab 管理方式。USB 接收保留为 Inspector 中的可选模式。

硬件组尚未确认实际 BLE 固件。下面是本仓库提供的**参考协议**，不是已从设备读取的参数。软件编译、模拟通信和接收器生命周期检查不代表实机蓝牙已连通。手机、macOS、Linux、Windows ARM64 暂未实现；没有使用经典蓝牙 SPP/虚拟 COM。

## 需要硬件组提供或确认

1. 烧录的固件/源码、Arduino-ESP32 版本，以及广播的完整设备名。
2. Service UUID、向电脑发送数据的 Notify Characteristic UUID、电脑写入命令的 Write Characteristic UUID。说明属性是否为 Notify、Write with response。
3. 至少三组实际蓝牙数据：静息、吸气、呼气。说明是文本还是二进制、数值单位（原始 ADC 毫伏/原始 ADC 计数/压力）、换行符和发送频率；最好保留原始字节或十六进制样例。
4. 是否支持下面的 HELLO/IDENTITY 握手；是否需要配对/PIN/加密；断线后是否恢复广播，是否已有手机占用连接。
5. 多台同名设备并存时，提供需要连接的蓝牙地址，或为每块板设置不同广播名。

如果采用仓库参考固件，以上名称、UUID、文本格式和握手可直接按下一节统一。如果硬件已使用其他固件，先核对协议再填参数。**仅修改 UUID 不能兼容二进制、无换行、无握手或只有 Notify 而没有 Write 的固件**；此类情况需要再调整协议适配代码。

## 参考 BLE 协议

- 外设：ESP32-C3；中心设备：Windows 电脑。
- 广播名称：`WingSinger Breath`，同时广播 Service UUID；名称可以放在扫描响应中。
- Service：`6e400001-b5a3-f393-e0a9-e50e24dcca9e`。
- RX（**从板子视角接收**，Unity 写命令，Write with response）：`6e400002-b5a3-f393-e0a9-e50e24dcca9e`。
- TX（**从板子视角发送**，Unity 订阅 Notify）：`6e400003-b5a3-f393-e0a9-e50e24dcca9e`。
- Unity 先订阅 TX，再向 RX 写 ASCII `WINGSINGER_HELLO` 加实际 LF。
- 板子经 TX 回复 ASCII `WINGSINGER_BREATH_V1` 加 LF，随后每 100ms 通知一行 GPIO0 原始 ADC 毫伏值，例如 `350\n`、`700\n`、`1200\n`。这里 `\n` 表示一个换行字节，不是反斜杠和字母 n。
- 默认 ATT MTU 23 只有 20 字节有效负载，21 字节的标识行会拆成两个通知。接收器按换行组装，不把“一个通知”当成“一整行”。支持 CRLF、拆包和合包；过长或损坏的行丢弃至下一换行。
- 持续发送数值，即使数值不变。未握手前不接受样本。每次重连重新订阅并握手。
- BLE 没有本项目需要配置的串口波特率。**115200 仅用于上传/USB 调试**。

上述 UUID 沿用 Espressif BLE UART 示例的服务布局；设备识别还要求指定名称（可选地址）与 WingSinger 握手，其他 UART 外设不会仅因发送数字而获得控制权。

## 建立连接

1. 硬件组确认已有协议，或使用 `Firmware/WingSingerBreathSensorBLE/WingSingerBreathSensorBLE.ino` 参考固件。参考固件需保留旁边的 `WingSingerBreathSensor` 文件夹，它复用原采样/校准逻辑。Arduino IDE 选择 ESP32C3 Dev Module、USB CDC On Boot = Enabled，Upload 后 RESET。此次修改没有自动向设备烧录。
2. 给板子供电，上电约 2 秒保持两气嘴无压差以完成原有校准。打开电脑蓝牙，确认蓝牙适配器及驱动支持 BLE；关闭手机 BLE 调试软件或其他占用这块板的程序。
3. 参考固件不要求预先在 Windows 设置里配对，Unity 通过广播直接发现并连接；不用创建 COM 端口。已有固件若要求配对/加密，应先按硬件组约定完成配对。
4. 打开 `Assets/Scenes/BreathDemo.unity`，选择 `GameRoot` 的 `ArduinoBreathInput`。`Connection Kind` 为 `Bluetooth LE`，核对 `Bluetooth Device Name` 和三个 UUID。`Bluetooth Address` 默认留空；多台设备时填写指定地址，如 `AA:BB:CC:DD:EE:FF`。参数更改后重新连接才生效。
5. Play。接收器会自动扫描、连接、订阅、握手；也可在现有控制器页面点检查连接。正确状态为 `Connected: Receiving`、`Fresh Sample=true`，界面显示 `BLE connected`。`Connected Port` 字段为兼容现有引用而保留名称，BLE 时显示 `BLE: 设备名`，不是 COM 号。
6. 勾选 `开启 Console 调试` 核对实际新样本及轨道：**≤400 吸气/上轨，≥1000 呼气/下轨，两者之间静息/中轨**。阈值可在原 Inspector 中实时调整。若单位不再是 ADC 毫伏，先修改解析/单位约定，不能直接沿用数值。
7. 实测三态后，关闭再开启板子电源，检查能回中轨并自动重连；再检查暂停、重开、切关和退出 Play。停止游戏应释放 BLE 连接，板子恢复广播。

可从 `WingSinger > Connect Bluetooth Input` 将已保存、非运行状态的 BreathDemo 重新接到 BLE 输入。原 `Connect Arduino Input` 菜单作为兼容入口也会应用当前 BLE 默认设置。需要 USB 调试时，在组件里改为 `Usb Serial` 并重新连接；自动 USB 扫描仍跳过蓝牙虚拟 COM。

## 超时与故障排查

- 默认 BLE 总连接/识别等待 30 秒；每个原生扫描/异步 GATT 步骤最多等待约 8 秒。失败后默认 2 秒重试。不要每帧手动重连，连接检查按钮即可重启一次接收器。
- 最新有效样本超过 0.5 秒回中轨；已连接但数据陈旧时不让键盘覆盖。连续 2 秒没有有效样本则断开重连。断线后可使用原 W/S 键盘后备。
- 搜索不到：检查广播名和 Service UUID（包含扫描响应）、电源、电脑蓝牙和手机连接占用。只有设备在系统列表中出现不代表它提供正确的 GATT 服务。
- 发现了但无法连接：检查操作系统/蓝牙驱动、配对要求及其他连接；看组件 `Connection Status` 的错误。固件修改过 GATT 服务后仍使用缓存旧服务时，可以在 Windows 删除旧配对记录后按新协议重新配对。
- 一直识别/握手超时：确认写入方向为 RX、通知方向为 TX，先开启 Notify，再写带真实换行的 HELLO，并收到完整标识行。
- 连接成功但一直无新数据：检查实际发送频率、纯数值格式、校准是否完成。不要通过反复读取本地缓存旧值来伪造“新样本”。
- 始终判呼气：检查静息原始 ADC。历史 USB 实测曾固定约 2875 mV，按默认 ≥1000 本来就会落到下轨；板内基线校准不会自动改变 Unity 的原始 ADC 阈值。三态阈值必须与硬件实测对应。
- 电量继续显示 `--`。界面的 `BLE` 表示连接类型，不虚构 RSSI 或电量。

## 软件实现、构建与验证

- `WindowsBleTransport`：适配现有后台接收器，Windows x64 P/Invoke。原生 `WingSingerBle.dll` 使用 Windows 自带 WinRT BLE API 进行主动扫描、服务发现、订阅和带响应写入，不依赖 Python、串口 DLL 或另一个后台程序。
- 原生源码在 `Tools/BluetoothNative/WingSingerBle.cpp`，执行 `Tools/BluetoothNative/Build.ps1` 可用 Visual Studio C++ 工具链及 Windows SDK 重建。DLL 约 190 KiB，静态 CRT；Unity 导入配置只启用 Windows x64 Editor/Player。
- C++ 接收队列有上限；溢出会断开重连，避免丢掉半行产生错误数值。异步回调持有弱引用，不从原生线程回调 Unity 对象。退出会停止扫描、取消等待、撤销通知处理器并关闭 GATT 引用；Windows 无线栈底层连接尝试可能稍后才结束，应用不继续使用旧会话。
- `ArduinoBreathInput` 保留原组件/GUID 与场景/UI 引用；BLE 和 USB 复用身份校验、有限数值解析、阈值和陈旧数据处理。公共输入不随关卡根对象关闭，暂停仍接收新样本。
- `dotnet run --project Tools/SensorInputTests/SensorInputTests.csproj`：原接收器回归、BLE 参数/阈值/陈旧样本、实际 DLL 搜索取消和反复释放。
- `Tools/FirmwareTests/Run.ps1`：原 USB 26 项，加 BLE 固件的握手、分包、ADC、断线广播等 14 项主机检查。此处无线 API 是测试替身，**不是 ESP32 目标编译或实机验证**。
- `WingSinger > Validate Bluetooth Input`：实际 Unity Mono 加载 DLL/启动取消扫描，再以模拟 BLE 字节流在正式场景执行定向检查，覆盖连接状态、换轨、暂停、重开、切关、超时重连和退出。报告在 `Logs/BluetoothNativeValidation.txt` 与 `Docs/Validation/BluetoothInputValidation.json`。
- 扩展的全导航回归曾在原有 `Pointer exit restores the exact authored transform` 按钮动画断言处失败，尚未定位；记录在 `Logs/BluetoothLegacyNavigationFailure.json`，不是 BLE 数据接收失败。此次保留原 UI 动画实现和全导航验证源码，单独完成 BLE 定向验证，不把全 UI 回归声称为通过。
- 此次环境没有 Arduino-ESP32 工具链/开发板连接，参考 BLE 固件的 ESP32-C3 编译、上传、真实广播/GATT/通知与断电重连需硬件组完成；不将软件测试表述为实机连通。

参考：[Espressif ESP32-C3 BLE 能力](https://docs.espressif.com/projects/esp-idf/en/latest/esp32c3/api-guides/ble/overview.html)、[Espressif Arduino BLE UART 示例](https://github.com/espressif/arduino-esp32/blob/3.3.2/libraries/BLE/examples/UART/UART.ino)、[Microsoft Windows GATT Client](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/gatt-client)。
