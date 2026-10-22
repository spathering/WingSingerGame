# Arduino 呼吸输入

当前示例场景 `Assets/Scenes/BreathDemo.unity` 使用共享 `GameRoot` 上的 `ArduinoBreathInput`，由同一个 `BreathLevelManager.input` 读取。Windows 编辑器和 Windows 桌面版支持 USB 串口；其他平台暂时只使用键盘后备输入。

## 固定协议与映射

- 波特率 115200，8 个数据位、无校验、1 个停止位（8N1），无流控。
- Unity 发送 `WINGSINGER_HELLO\n`，Arduino 回复 `WINGSINGER_BREATH_V1\n`。允许 CRLF。
- 识别成功后才接受数据：每行一个十进制数值，例如 `350\r\n`、`700\r\n`、`1200\r\n`。解析使用小数点；不接受标签、CSV、JSON、NaN 或 Infinity。
- 数值 **≤400**：吸气，等效 W，前往上轨。
- 数值 **≥1000**：呼气，等效 S，前往下轨。
- **400 < 数值 < 1000**：无输入，回到中轨。边界 400 和 1000 分别包含在吸气与呼气范围内。

硬件组已确认该协议。映射默认不做平均或滞回处理，避免改变已确认的三个数值区间；后续若实测抖动，再单独调整。

## 使用方式

1. 上传支持上述握手的固件，使用 USB 数据线连接 Arduino。
2. 关闭占用该串口的 Arduino Serial Monitor/Plotter 等程序，再运行 BreathDemo。
3. `Port Override` 留空：自动枚举非蓝牙 COM 串口，逐个握手，找到匹配设备后接收。串口号变化后也会重新发现，不固定 COM3/COM4。可填写已知 COM 号限制检测范围，但仍须通过握手。
4. 在 `GameRoot > ArduinoBreathInput` 的 Play Mode Inspector 查看 Connection Status、Connected Port、Latest Value 和 Fresh Sample。没有新增游戏 UI。
5. 默认启用 `Keyboard When Disconnected`，没有连接设备时可继续用 W/S 开发；传感器连接后优先使用传感器。需要严格的传感器模式时关闭该选项。

## 阈值调整与 Console 调试

在 `GameRoot > ArduinoBreathInput > 呼吸阈值与调试（实时生效）` 中统一设置：

- **吸气阈值（≤，上轨）**：默认 400 mV。
- **呼气阈值（≥，下轨）**：默认 1000 mV。
- **开启 Console 调试**：默认关闭。开启后输出最新接收的 ADC 毫伏值、吸气/呼气/静息状态、轨道和当前阈值，例如 `[WingSinger Arduino] COM13 | 数值=700 mV | 状态=静息 / 中轨 | 吸气≤400，呼气≥1000`。

两个阈值及调试开关可以在 Play 中直接调整，立即生效，无需重连或重新烧录。吸气阈值必须小于呼气阈值；相等、颠倒或非有限值会显示“阈值无效（保持中轨）”，保持串口连接以便修正参数。Play 中调整的 Inspector 参数退出后会恢复；需要保留时，在停止 Play 后填写并保存场景。

日志在主线程生成，同一接收样本不会每帧重复输出；连续收到相同数值仍视为新样本。接收器保留最新值，帧间有多个样本时只记录最新样本，不积压历史日志。过期/断线时只输出状态变化，不把旧值当作新数据。开场预览期间也可看日志，但玩家仍按原有设计保持中轨。

`Live receiver status` 中新增“当前呼吸状态”。调试状态与游戏输入使用同一套阈值判定；这里的“静息”表示数值处于中间区间，不代表自动校准了硬件静息基线。

识别等待默认 4 秒，并每 0.5 秒重发握手，以兼容打开串口可能引起的 Arduino 重启。0.5 秒没有有效数值就回到中轨；连续 2 秒没有有效数值则关闭当前连接并重新扫描，重试间隔 2 秒。断线时若启用键盘后备，W/S 仍可操作。建议固件持续输出，例如每 20 毫秒一行，而非仅在数值改变时输出。

运行时更改串口或连接时序参数，需要关闭、重新启用该组件，让接收器使用新参数。吸气/呼气阈值可在 Inspector 调整；默认按硬件组约定保持 400/1000。

## 生命周期与实现

- `BreathSensorProtocol`：纯数值映射与有长度限制的换行解码，支持拆包、合包、CRLF，拒绝损坏和过长的数据行。
- `BreathSensorClient`：后台扫描、握手、接收和重连，只保留最新数值；后台不访问 Unity 对象。
- `WindowsSerialTransport`：使用 Windows 的 COM 设备和串口 API，保持现有 .NET Standard 配置，不引入串口 DLL、第三方依赖或程序集边界变化。跳过蓝牙虚拟串口，以免等待远端蓝牙连接。Win32 参考：[设备句柄](https://learn.microsoft.com/en-us/windows/win32/devio/communications-resource-handles)、[串口超时](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-commtimeouts)、[DCB](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-dcb)。
- `ArduinoBreathInput`：主线程根据最新有效数值返回逻辑轨道，继承现有 `BreathInput`。原有 `ExternalBreathInput.Submit` 继续供其他外部输入适配器使用。
- 接收器位于关卡根对象之外，重开和切换 Prefab 不重建连接；组件关闭、退出 Play Mode 或程序退出时取消接收并释放串口。重新启用使用新接收器，不复用旧读数。
- 开场预览仍保持中轨，正式进入玩法后使用传感器；角色的入场衔接、跨轨动画和纸星星计分规则保持原设计。

菜单 `WingSinger > Connect Arduino Input` 将已有并已保存的 BreathDemo 接到该输入组件；拒绝覆盖未保存的场景。新建示例的生成工具也会绑定同一组件。

## 验证

纯逻辑与后台接收测试：

```powershell
dotnet run --project Tools/SensorInputTests/SensorInputTests.csproj -c Release
```

编辑器菜单 `WingSinger > Validate Arduino Input` 使用模拟串口传输驱动真实玩家，验证阈值、握手、换轨、数据超时、重开、切关与接收器清理。报告输出到 `Logs/ArduinoInputValidation.txt`。只在测试期间临时允许后台运行，结束后恢复，不修改 Player Settings。

当前电脑仅检测到蓝牙串口，尚未连接 Arduino。软件模拟验证不代表已经完成真实硬件端到端测试；设备连接后需核对识别成功、三档数值控制、拔插重连与退出释放端口。

2026-10-22 已通过 57 项协议/接收线程测试，以及真实示例场景内的 19 项模拟串口 Play Mode 检查，报告见 [ArduinoInputValidation.txt](ArduinoInputValidation.txt)。通过当前项目的 Unity MCP 定向导入本次脚本，复用现有编辑器与缓存，仅编译 Main 及 Editor 依赖；未更改 .NET 配置、渲染管线或包依赖。

2026-10-22 调试开关与运行时阈值调整更新：控制台套件现已通过 86 项检查，新增覆盖配置阈值、样本序号、日志启停、相同值的新样本、逐帧去重、状态一致性和数据过期。组件行为测试使用轻量 Unity API 替身运行真实组件和接收器，不代表 Play Mode 测试。另用 Unity 2022.3.62f2 自带编译器与现有 Main 程序集响应文件验证通过，输出仅在 Temp 中；本轮未执行新的编辑器 Play Mode 验证。
