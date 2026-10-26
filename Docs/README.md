# 文档索引

## 设计

- [UI 页面、导航与生产接入边界](Design/UI/UIFlowDesign.md)：13 个页面、返回规则、场景关系和参考图差异。
- [UI 素材与 Prefab 规范](Art/UIAssetGuide.md)：切分来源、通用组件、九宫格、字体和重建方式。
- [基础飞行玩法](Design/BasicGameplay.md)。
- [纸制收集物规则](Design/PaperCollection.md)。

## 工程与素材

- [开发指南](DevelopmentGuidelines.md)：固定工程基线与增量编译约定。
- [工程目录整理说明](Development/ProjectStructure.md)、[迁移清单与保留 GUID](Development/StructureMigration.json)。
- [PrimeTween 按钮交互与关卡 SO 目录](Development/UIInteractionsAndLevelCatalog.md)：现有两关、可选关卡配置和接线/验证入口。
- [UI 窗口与 16:9 适配](Development/UIViewportLayout.md)：Canvas 基准、页面等比适配和多尺寸验证。
- [原有角色和场景素材](Art/ArtAssets.md)。
- [两层循环视差背景](Art/ParallaxBackground.md)：主分支合入的天空与透明前景、参数及合并验证。
- [Unity MCP 配置记录](Development/UnityMcpSetup.md)：历史端口与路径需要现场核实。

## 硬件

- [Arduino 输入协议与调试](Hardware/ArduinoInput.md)。
- [硬件与串口联调上下文](Hardware/HardwareIntegrationContext.md)：原 WINGSINGER_91E5B8BCE69F99E9B99BE48AB8E687 文档，保留历史实测和问题记录。
- [Arduino 接手上下文](Handoffs/ArduinoInputContext.md)：历史交接材料，最新实现以代码、固件说明为准。
- [当前 ESP32 固件](../Firmware/WingSingerBreathSensor/README.md)。

## 验证与参考

- [本轮验收汇总](Validation/UIValidationSummary.md)。
- [UI 构建验证](Validation/UIBuildValidation.json)。
- [UI 实际 Play Mode 验证](Validation/UIPlayValidation.json)、[目录/资源/链接验证](Validation/ProjectOrganizationValidation.json)。
- [正式导航与玩法验证](Validation/GameplayNavigationValidation.json)、[主场景运行截图](Design/UI/RuntimePreviews/)。
- [历史报告目录](Validation/)：报告中的日期、环境和模拟/真实硬件范围仍按原记录解释。
- [13 张参考图](Design/UI/References/) 和 [Unity 渲染结果](Design/UI/Previews/)。
- [原有玩法截图](Images/)。
