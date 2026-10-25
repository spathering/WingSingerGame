# ESP32-C3 与 Unity 呼吸输入固件

在 Arduino IDE 打开同目录的 `WingSingerBreathSensor.ino`。

## 烧录设置

- 开发板：esp32 by Espressif Systems → ESP32C3 Dev Module。
- USB CDC On Boot：Enabled。
- 端口：选择当前 ESP32 对应的 COM 端口；本次识别为 COM13，重插后可能改变。
- 使用 Upload 烧录。烧录完成后如果仍处于下载模式，按一下 RESET 运行程序。
- Unity 测试前关闭 Arduino Serial Monitor / Plotter，避免占用串口。

## 数据约定

115200、8N1、无流控。GPIO0 每 10ms 采样；收到 `WINGSINGER_HELLO` 加实际换行后，立即回复 `WINGSINGER_BREATH_V1` 加换行。基线校准完成后每 100ms 输出一行 ADC 毫伏整数。每次重复收到 HELLO 都回复标识，支持 Unity 重连；重新握手不会重新校准。

启动时不输出数字、标题或 CSV。握手且校准完成后持续输出，即使数值未变化。接收命令支持 LF / CRLF、拆包和合包；超长或损坏命令丢弃至下一换行。采样、校准和发送均按 `millis()` 调度，不使用 `delay()`。

默认串口输出仍是分压后 GPIO0 的原始 `analogReadMilliVolts()` 结果，与用户原代码最后实际启用的 `Serial.print(adcMv)` 一致；不是板内滤波值或压力值。Unity 继续按组件中可调的 ADC 毫伏阈值映射，默认 ≤400 上轨、≥1000 下轨、其余中轨。

## 恢复的原有传感器处理

- 传感器输出估算：`sensorMv = adcMv * 2.0`。
- 上电先等待约 1500ms，再按 20ms 间隔采集 10 个传感器电压值取平均作为 `baselineMv`。启动后的约 2 秒保持两气嘴无压差，不吹吸。
- 校准完成后将 `filteredMv` 初始化为基线，后续每 10ms 执行 `filteredMv += 0.25 * (sensorMv - filteredMv)`。
- 相对压力估算：`deltaKPa = (filteredMv - baselineMv) / 1000`，保留原参数的标称估算，不代表已校准的压力测量。
- 保留原 `classifyBreathing` 逻辑：REST 时 ≥0.08 进入 EXHALE、≤-0.08 进入 INHALE，活动状态在绝对值 ≤0.03 时退出到 REST。`INVERT_DIRECTION=false`，可按实际气路方向修改。
- 去除原循环内的 `delay(100)`，使 10ms 采样不再被串口打印阻塞；校准期间仍可处理握手命令。

板内压力状态与 Unity 的 ADC 阈值判断是两套不同依据：例如静置 ADC 为 2875，板内基线校准后可能为 REST，但 Unity 默认阈值仍会判为呼气/下轨。需要调整 Unity 阈值或明确改用压力协议时再修改控制依据，不能将这两种状态混同。

## 状态查询与重新校准

在 Arduino 串口监视器中选择 115200 和“新行”，可发送：

| 命令 | 响应 |
| --- | --- |
| `WINGSINGER_HELLO` | 回复设备标识并在校准完成后连续发送原始 ADC 数值 |
| `WINGSINGER_STATUS` | 查询时间、原始 ADC、估计传感器电压、基线、滤波值、相对压力及 REST/INHALE/EXHALE；校准中为 CALIBRATING |
| `WINGSINGER_CALIBRATE` | 回复 WINGSINGER_CALIBRATING，按 10 次/20ms 重新采集基线；期间暂停数字输出，完成后自动恢复 |

状态与校准响应有明确文本前缀，Unity 现有纯数值解析器会忽略它们，不将文本当成输入样本。默认不自动发送诊断行。重新校准前保持两气嘴无压差。

可在串口监视器中选择 115200 和“新行”，发送 `WINGSINGER_HELLO` 检查标识及数值。Unity Play 前必须关闭监视器。

## 接线与验证范围

沿用 [硬件文档](../../Docs/Hardware/HardwareIntegrationContext.md) 的 GPIO0 与分压接线。传感器 OUT 不可直接接到 GPIO0；调整接线前断开 USB。

本程序整合通信协议与原有传感器处理。实际呼吸方向、静置基线和 ADC 大幅跳变仍需实测，不能仅凭数字出现或滤波后稳定就判定呼吸映射正常。

原有 `Assets/Scripts/Hardware/sketch_oct22a/sketch_oct22a.ino` 保留；其无握手输出不能通过当前 Unity 的设备识别。

此前通信版（2026-10-22）：使用本机 Arduino-ESP32 3.3.12，目标 `esp32:esp32:esp32c3:CDCOnBoot=cdc` 编译通过，已按用户授权烧录到 COM13，写入校验通过。实机返回正确握手标识，项目原有接收器进入 `Connected / Receiving`。随后约 10 秒采样观察均为 2875，按默认阈值映射下轨；未完成有明确动作标记的吸气/静息/呼气三态验证。该记录属于整合前版本，不代表整合版已通过实机验证。

整合版（2026-10-22）：26 项主机逻辑检查通过；上述 ESP32-C3 目标实际编译通过，程序占用 311928 字节，全局变量占用 14792 字节。尝试烧录时在串口配置阶段遇到 Windows 错误 31，未开始写入；当前整合版尚未烧录到硬件。
