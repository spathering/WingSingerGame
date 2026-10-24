# WINGSINGER 呼吸训练游戏｜硬件与串口联调上下文

> 整理日期：2026-10-22。本文基于本次聊天中的已知信息，供后续聊天、硬件组及 Unity 软件组继续联调。文中样例值不代表经过校准的临床测量。

## 1. 项目目标

开发面向哮喘呼吸训练场景的交互式游戏原型。使用差压传感器捕捉呼气、吸气及压力变化，ESP32-C3 读取信号，先通过 USB 串口与 Unity 游戏通信，后续可考虑 BLE。原型不是医疗诊断或治疗设备。

## 2. 已选硬件

- 主控：**ESP32-C3 SuperMini**，Arduino IDE 通常选择 **ESP32C3 Dev Module**。
- 传感器：紫色 **MPXV7002DP** 差压模块（用户描述为 VCC、GND、OUT 三针与两个气嘴；具体模块电路尚未通过原理图确认）。
- 面包板：400 孔足够初期搭建。
- 两个 10 kΩ 电阻组成 1:2 模拟信号分压，杜邦线、USB 数据线。
- 供电：传感器模块 VCC 接开发板 5V/VBUS（需要确认模块额定输入）；共同 GND。

### 接线结构

```text
ESP32-C3 SuperMini                 MPXV7002DP 模块
5V / VBUS ------------------------ VCC
GND ------------------------------ GND
                                   OUT
                                    |
                               R1 10 kΩ
                                    |
                         +----------+------- GPIO0 (ADC1_CH0)
                         |
                       R2 10 kΩ
                         |
                        GND
```

必须检查面包板孔位内部的实际导通关系、供电轨是否断开、传感器与分压电阻是否可靠共地。**不可把可能高达约 4.5V 的 OUT 直接接 ESP32 的 GPIO0**。在重新接线前断电。两只相等电阻的分压节点应是传感器输出电压的一半。

### 气路

差压传感器比较两个气嘴的气压。此前测试时用户对其中一个气嘴施压，另一个敞开；若要实现真正的呼吸游戏，需要安全、可清洁、低阻力且避免冷凝水进入传感器的气路。不要直接对着电路板吹气，不要用力向封闭气路吹吸。方向标识需根据实际气路标定。

## 3. 开发环境与串口

- Windows 新电脑已成功识别 ESP32-C3，烧录日志曾显示 **COM7**，但 COM 号可随重插改变。
- Arduino 开发板支持包：**esp32 by Espressif Systems**；开发板选 **ESP32C3 Dev Module**（不要选择 Arduino Nano ESP32）。
- ESP32-C3 原生 USB：**Tools → USB CDC On Boot → Enabled**；修改后重新上传。否则可能上传成功但 Serial Monitor 完全空白。
- UART/USB 串口参数：**115200 波特率，8N1**。
- Arduino IDE 的 **Upload**（向右箭头）用于烧录；不需要点击 **Debug**（虫子图标）。后者会启动 OpenOCD/GDB，曾出现 `GDB Server Quit Unexpectedly`。
- **同一 COM 端口不可同时由 Unity 与 Arduino Serial Monitor 占用**；Unity 测试前关闭串口监视器，出现 `PermissionError(13), Could not open COM7, port busy or doesn't exist` 时排查占用和 COM 号变化。

## 4. 测量和代码参数沿革

原型使用 `analogReadMilliVolts(GPIO0)` 读取 ADC 引脚电压（单位 mV），不是传感器未经分压的电压。若确实为 1:2 分压，则估计传感器输出 `sensor_mV = adc_mV * 2`。以 MPXV7002DP 在 5V 供电时的标称约 1 V/kPa 估计 `delta_kPa = (filtered_sensor_mV - baseline_sensor_mV) / 1000`。**这是未校准的相对压力估算，不可用于临床判断**。

已出现过的关键版本：

1. 早期五列 CSV：`time_ms,adc_mV,sensor_mV,delta_kPa,state`；`REST / INHALE / EXHALE` 状态；后来改为每 10ms 采样、每 100ms 串口输出。
2. 用户当时自己正在用的版本：`SAMPLE_INTERVAL_MS=10`、`PRINT_INTERVAL_MS=100`、`FILTER_ALPHA=0.25`、`ENTER_THRESHOLD_KPA=0.08`、`EXIT_THRESHOLD_KPA=0.03`、`INVERT_DIRECTION=false`、启动校准 10 次每次间隔 20ms。输出被改成 **每行仅一个原始 `adcMv` 数字**，并在输出后加入 `delay(50)`；讨论指出 `delay(50)` 会阻塞采样，应删除。
3. 后续为了 Unity 自动识别，提供了在原参数基础上新增串口握手的整合版本，默认握手后才输出单列 `adcMv` 数值；没有握手则不发送数字。
4. 为在 Arduino IDE 单独测试，又提供了**无握手、上电后自动输出 `adcMv`** 的简化代码：每 10ms 采样，每 100ms 输出一次，115200 波特率，不滤波、不做呼吸分类；开头会输出 `Breathing Sensor Test` 和 `ADC_mV` 两行标题。注意若软件组解析为纯数字，这两行标题需过滤或取消。

### 简化的无握手、数字输出测试程序（最后一次提供的 Arduino 测试版）

```cpp
#include <Arduino.h>

const int SENSOR_PIN = 0;
const unsigned long SAMPLE_INTERVAL_MS = 10;
const unsigned long PRINT_INTERVAL_MS = 100;
uint32_t adcMv = 0;
unsigned long lastSample = 0;
unsigned long lastPrint = 0;

void setup() {
  Serial.begin(115200);
  delay(1500);
  analogReadResolution(12);
  analogSetPinAttenuation(SENSOR_PIN, ADC_11db);
  Serial.println("Breathing Sensor Test");
  Serial.println("ADC_mV");
}

void loop() {
  unsigned long now = millis();
  if (now - lastSample >= SAMPLE_INTERVAL_MS) {
    lastSample = now;
    adcMv = analogReadMilliVolts(SENSOR_PIN);
  }
  if (now - lastPrint >= PRINT_INTERVAL_MS) {
    lastPrint = now;
    Serial.println(adcMv);
  }
}
```

## 5. Unity 与硬件组商定的可选握手协议

Unity 团队希望不用固定 COM7，而使用 `SerialPort.GetPortNames()` 枚举端口并握手识别：

- Unity 打开候选端口（115200，8N1）后发送一行 `WINGSINGER_HELLO\n`。
- ESP32 收到完整命令回复 `WINGSINGER_BREATH_V1\n`。
- 握手成功后 ESP32 才以约 10 Hz 连续发送**一行一个 ADC 毫伏整数**，例如 `680\n`；这是兼容用户既有 Unity 单列数值读取设计的版本。
- ESP32 内部采样约 100 Hz（10 ms）。
- Unity 掉线后扫描端口并重新握手；多个设备匹配时允许用户手动选择。Arduino IDE 的 Serial Monitor 不可与 Unity 同时占用同一个串口。
- 注意：这仅是已设计并提供代码的协议；**当前实际 ESP32 上烧录的是哪一个版本，需现场核实**。没有握手的 Arduino 测试版不会回应 HELLO；有握手版本上电后不会自动发送数值。

## 6. 已记录实测数据及现象

### 曾经能明显感知吹气

用户提供的静置 ADC 输入毫伏值：

```text
217445,679,1358.0
217545,686,1372.0
217645,668,1336.0
217745,685,1370.0
217845,686,1372.0
217945,685,1370.0
218045,673,1346.0
218145,677,1354.0
```

吹气时：

```text
248925,1046,2092.0
249025,1075,2150.0
249125,1091,2182.0
249225,1138,2276.0
```

这些数据证明 ADC 读数可以随施压明显变化，但**不证明整个电路始终接线正确或压力已经校准**。

### 最新主要未解决问题：静置时 ADC 在 0–3000mV 间剧烈甚至周期性跳动

新电脑恢复串口输出之后，截图中静置数值有约 1046–1160mV 的一组，也曾出现约 2439–2702mV，之后用户反馈在完全静止、不吹气、不触碰线路时仍会在 0–3000mV 间剧烈跳动。

在 **5V 供电、MPXV7002DP 零差压输出约 2.5V、1:2 分压有效** 这个前提下，GPIO0 上的静态电压预期接近 1250mV；0–3000mV 大幅乱跳不是正常静息呼吸信号，部分高值还可能处于 ESP32-C3 ADC 的不准确测量范围。**不要通过调滤波或阈值掩盖它**。

已提出但尚未收到结果的隔离测试：

1. **断开 USB 供电**，把 GPIO0 与传感器 OUT/分压节点彻底断开。
2. 仅将 ESP32 GPIO0 通过杜邦线接 **ESP32 自己的 GND**（不能连 5V/3.3V），再供电运行 ADC 测试程序，期望约 0mV 且相对稳定。这只验证 ADC 接地输入基础功能。
3. 若上述稳定，再断电恢复分压接线，用万用表检查 VCC→GND 约 5V、模块 OUT→GND 约 2.5V、GPIO0→GND 约 1.25V（均取决于模块实际电路），确认共地和面包板导通；GPIO0 若超过允许电压，立即断电。
4. OUT 稳定而分压节点不稳 → 查电阻和接线；OUT 自身不稳 → 查供电、地线和模块；GPIO0 接地仍跳动 → 查开发板、引脚选择和测试线。

## 7. 最近一次联调状态

- 用户开始与 Unity 软件组一起调试。
- 遇到串口被占用或消失的 COM7 报错，以及误启动 OpenOCD 调试时的 `GDB Server Quit Unexpectedly`。
- 已明确：**Arduino IDE 只需 Upload 烧录，Unity 运行 Play；不要点 Arduino Debug**。
- Unity 运行前关闭 Arduino Serial Monitor，确认实际烧录版本与 Unity 的握手/数据协议一致。
- **静置时 0–3000mV 的硬件异常尚未确认修复**；应优先完成 GPIO0 接地隔离测试与电源/分压检查，然后再评估 Unity 检测效果。

## 8. 下一步建议

1. 确认当前实际运行的是 Arduino 无握手测试版，还是 WINGSINGER 握手版，避免 Unity 等待不可能收到的标识或等待永远不开始的数值。
2. 检查 GPIO0 → GND 隔离测试是否稳定，必要时测量 OUT 与分压节点，定位静置大幅跳动原因。
3. 硬件稳定后记录静置、呼气、吸气三个方向的原始时间序列，做压力基线、动态阈值和呼吸方向映射。
4. 最终确定 Unity 通信协议（推荐固定波特率、握手标识、显式数据版本、时间戳、状态及错误处理）；需要更高交互速度时可以提高输出频率，但与团队一起确认。
5. 在任何用户口含气路测试前检查卫生、气流阻力、冷凝保护及安全性。未经验证不得作为哮喘诊疗设备。

## 9. 待确认事项

- 紫色 MPXV7002DP 模块的具体生产商与电路原理图（是否已集成分压/调理）。
- 当前分压器在面包板上的具体插孔和电阻实际阻值。
- GPIO0 接 GND 单独测试结果、传感器供电与 OUT 实测电压。
- Unity 项目当前约定是纯数字自动发送还是 HELLO 握手；是否已导入对应解析代码。
- 当前固件的版本及 USB CDC On Boot 设置。
