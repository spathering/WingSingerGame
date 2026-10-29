# 整合固件的主机逻辑检查

Windows 安装 Visual Studio C++ 工具链后可运行 `Tools/FirmwareTests/Run.ps1`，同时执行下面的原 USB 26 项检查及新增 BLE 14 项主机检查。BLE 测试使用 `BLE/` 中的无线 API 替身，只验证实际 BLE `.ino` 的逻辑，不代表 ESP32 目标编译或射频通信通过。

从项目根目录运行（本机提供 g++）：

```powershell
g++ -std=c++11 -Wall -Wextra -pedantic -I Tools/FirmwareTests Tools/FirmwareTests/WingSingerBreathSensorTests.cpp -o Temp/WingSingerFirmwareTests.exe
.\Temp\WingSingerFirmwareTests.exe
```

测试直接包含实际 `.ino`，以可控 ADC、时钟和串口替换 Arduino API，检查校准、滤波、原滞回分类、串口握手、原始数值输出、状态查询、重新校准及计时回绕。不验证电压测量精度或真实气路。

真实 ESP32-C3 编译目标：`esp32:esp32:esp32c3:CDCOnBoot=cdc`。主机测试通过不能替代该目标的实际编译、烧录及硬件联调。
