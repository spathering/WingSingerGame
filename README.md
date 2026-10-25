# WingSinger

Unity 呼吸输入与飞行训练游戏原型。使用 Unity **2022.3.62f2**、URP **14.0.12**。

## 从这里开始

| 目标 | 入口 |
| --- | --- |
| 浏览新 UI 和页面跳转 | 打开 `Assets/Scenes/UI/UIFlowPreview.unity`，Play |
| 使用 13 个独立页面 | `Assets/Prefabs/UI/Screens/` |
| 使用共用组件与完整 UI 根对象 | `Assets/Prefabs/UI/Shared/`、`Assets/Prefabs/UI/UIFlowRoot.prefab` |
| 从首页进入完整游戏流程 | `Assets/Scenes/BreathDemo.unity`，Play |
| 配置可选关卡和按钮效果 | [UI 交互与关卡目录](Docs/Development/UIInteractionsAndLevelCatalog.md) |
| 接入 ESP32 | [Arduino 输入](Docs/Hardware/ArduinoInput.md)、[固件](Firmware/WingSingerBreathSensor/README.md) |
| 查阅设计与工程说明 | [文档索引](Docs/README.md) |

主场景已接入 13 页导航：选关→起飞→暂停/继续/重开→结算，并保存成绩、解锁和设置。当前有两个真实关卡，吸气上轨、呼气下轨；未连接控制器时可使用 W/S 后备输入。独立 UI 预览场景继续展示参考图示例数据。详见 [UI 页面与导航设计](Docs/Design/UI/UIFlowDesign.md)。

## 工程目录

```text
Assets/Scripts/Input/                    输入、协议、串口与键盘后备
Assets/Scripts/Gameplay/{Core,Presentation,Collectibles}/
Assets/Scripts/Levels/                   单场景关卡与配置
Assets/Scripts/UI/{Navigation,Controls,Runtime,Legacy}/
Assets/Editor/{Setup,Validation,UI}/      编辑器接线、验证与 UI 构建
Assets/Art/UI/{Backgrounds,Sprites,Illustrations,Skins,Fonts}/
Assets/Prefabs/UI/{Screens,Shared}/
Docs/{Design,Art,Hardware,Development,Handoffs,Validation,Images}/
Firmware/                               ESP32 程序
Tools/{UI,SensorInputTests,FirmwareTests,CollectionLogicTests}/
```

关卡始终由同一主场景的 `BreathLevelManager` 通过 Prefab 实例 `SetActive` 切换。[开发约定](AGENTS.md) 和 [开发指南](Docs/DevelopmentGuidelines.md) 适用于全部修改。

## 构建与验证

- Unity 菜单 `WingSinger > UI > Build Reference UI`：按源码生成 UI 资源、页面、预览场景及 13 张截图。重建会覆盖该工具生成的 UI Prefab；需要自定义时先建立 Prefab Variant。
- Unity 菜单 `WingSinger > UI > Connect Navigation to Gameplay`：将公共 UI 根对象绑定到现有 `BreathDemo`；重新生成参考 UI 后使用此命令更新主场景绑定。先保存已打开场景。
- Unity 菜单 `WingSinger > UI > Apply Button Effects and Level Catalog`：添加 PrimeTween 按钮反馈，按关卡定义 SO 匹配现有场景实例；保留已有 UI 布局覆盖。现有两关初次进入均可选择。
- Unity 菜单 `WingSinger > UI > Apply Responsive Viewports`：统一 1920×1080 Canvas 基准并保留页面比例；`Validate Responsive Viewports` 检查多种窗口尺寸。见 [窗口适配说明](Docs/Development/UIViewportLayout.md)。
- Unity 菜单 `WingSinger > UI > Validate Gameplay Navigation`：在主场景检查导航和玩法，使用隔离存档与模拟串口，不打开实物串口。结果见 [导航运行验证](Docs/Validation/GameplayNavigationValidation.json)。
- UI 原图切分工具：`python Tools/UI/prepare_ui_assets.py "D:\Studies\ProjMgrt\wingsinger"`，依赖 Pillow 和 NumPy；外部原图只读。
- 输入逻辑检查：`dotnet run --project Tools/SensorInputTests/SensorInputTests.csproj -c Release`。
- 收集物逻辑检查：`dotnet run --project Tools/CollectionLogicTests/CollectionLogicTests.csproj -c Release`。
- 固件检查：[Tools/FirmwareTests](Tools/FirmwareTests/README.md)。
- [UI 验证报告](Docs/Validation/UIBuildValidation.json) 与 [实际渲染预览](Docs/Design/UI/Previews/)。

Unity UI 构建复用当前 Library 缓存和编译程序集，不要求重新导入插件、改变渲染配置或切换 .NET 平台。
