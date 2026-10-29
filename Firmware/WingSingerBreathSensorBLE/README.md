# ESP32-C3 BLE 参考固件

硬件组尚未确认现有 BLE 固件协议。本目录提供与游戏新 BLE 接收器匹配的参考实现，未自动烧录，尚未完成 ESP32 目标编译或实机验证。

在 Arduino IDE 中打开同目录 `WingSingerBreathSensorBLE.ino`，使用 Arduino-ESP32 3.x 的内置 BLE 库、ESP32C3 Dev Module、USB CDC On Boot = Enabled。保留旁边 `../WingSingerBreathSensor` 文件夹：BLE 版本直接复用该 USB 固件中的 ADC、校准和诊断实现；不能只复制本目录的 `.ino` 后单独编译。

广播 `WingSinger Breath`，使用 BLE UART Service `6e400001-b5a3-f393-e0a9-e50e24dcca9e`，RX Write 末尾 `0002`、TX Notify 末尾 `0003`（完整 UUID 见代码）。Unity 订阅 TX 后向 RX 写入 `WINGSINGER_HELLO\n`，收到 `WINGSINGER_BREATH_V1\n` 才接受数字。每 100ms 发送一行原始 ADC 毫伏值，分包不超过 20 字节，断开后恢复广播；不要求配对/PIN。

原有 GPIO0、分压、启动校准、滤波/压力诊断保持不变，USB 115200 仍可调试。BLE 发的是原始 ADC 毫伏，不是压力分类。默认 Unity 阈值仍为 ≤400 吸气、≥1000 呼气，静息实测值不同需要在 Unity 调整阈值。

BLE 回调只解析命令并设置原子标记，采样、发送和广播恢复在主循环处理；无需在蓝牙回调中操作校准状态。当前 BLE 入口只接受 HELLO，状态查询/重新校准继续走 USB，避免两条通信线程同时修改采样状态。

连接步骤、配置项、硬件组待确认清单和测试范围见 [BLE 输入文档](../../Docs/Hardware/BluetoothInput.md)。原采样/接线说明见 [USB 固件说明](../WingSingerBreathSensor/README.md)。
