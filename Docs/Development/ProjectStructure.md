# 工程结构整理（2026-10-25）

这次整理按职责归类第一方代码和文档，保留命名空间、类名、现有 `Main.asmdef`、第三方程序集、场景及关卡资源。

| 目录 | 责任 |
| --- | --- |
| `Assets/Scripts/Input` | BreathInput、Arduino 输入、解析协议、后台客户端、Windows BLE/USB 串口与键盘后备 |
| `Assets/Scripts/Gameplay/Core` | 纯时间、路线和计分会话模型 |
| `Assets/Scripts/Gameplay/Presentation` | 玩家表现、角色精灵动画、背景滚动 |
| `Assets/Scripts/Gameplay/Collectibles` | 收集物布局、表现与接触判定 |
| `Assets/Scripts/Levels` | 统一关卡管理、关卡实例、呼吸参数、关卡定义与目录 SO |
| `Assets/Scripts/UI/Navigation` | UI 页面标识、路由按钮、单页面激活管理 |
| `Assets/Scripts/UI/Controls` | 滑条百分比、开关文案、PrimeTween 按钮交互 |
| `Assets/Scripts/UI/Runtime` | 正式导航与会话桥接、真实 UI 数据、设置与本地成绩存储 |
| `Assets/Scripts/UI/Legacy` | 保留现有可选 BreathHud，实现和序列化类型不变 |
| `Assets/Editor/Setup` | 原有示例生成与资产接线工具 |
| `Assets/Editor/Validation` | 原有玩法、素材、串口模拟检查 |
| `Assets/Editor/UI` | 参考 UI 批量构建、导入和渲染验证 |
| `Assets/Art/UI` | 可复用 UI 纹理、字体和来源清单 |
| `Assets/Prefabs/UI` | 13 个页面、7 个共用组件和 UIFlowRoot |

## 引用安全

37 条路径整理记录见 [StructureMigration.json](StructureMigration.json)。Unity 脚本和对应 `.meta` 一起移动，现有场景/Prefab 的脚本 GUID 保持不变；不用移动后重挂组件。控制台检查项目的源码路径和文档链接同步更新。

`Assets/Scripts/Hardware/sketch_oct22a` 仍保留用户提供的原始 Arduino 草稿，便于追溯；当前可构建固件的唯一入口是 `Firmware/WingSingerBreathSensor`。两者的角色已在固件说明中区分。

本次没有改动 URP 资产、第三方插件源码、构建列表、输入物理方向或已有关卡管理方式。新增 UI 预览场景只用于查看 UI；普通游戏关卡不通过加载场景切换。

## 文档归类

- `Design`：玩法、收集和 UI 页面设计。
- `Art`：素材规范、来源及导入要求。
- `Hardware`：当前输入协议与原始硬件上下文。
- `Development`：工程结构与工具配置。
- `Handoffs`：接手材料；历史结论不代替当前验证。
- `Validation`：可追溯的报告。
- `Images`：原有玩法截图；UI 原图/渲染另放在 `Design/UI` 下。

开发入口仍是根目录 `AGENTS.md` 和 `Docs/DevelopmentGuidelines.md`，避免改变已有协作约定的查找位置。

## 导航接入后的增量修改

目录整理后，根据导航图扩展了 `BreathLevelManager` 的可控启动、暂停和退出接口，并在 `BreathPlayer`、`BreathLevel` 增加减少动画选项；三者保留原 GUID 和原有关卡实例管理方式。主场景新增公共 UI 实例和业务绑定，参考图页面资源继续用于画廊。源码内容校验对这三个明确修改的脚本检查 GUID，其他迁移脚本继续检查与迁移前内容相同。

2026-10-26 主分支背景集成另扩展 `Gameplay/Presentation/LoopingPanorama` 与 `Editor/Setup/BreathArtSetup`，承接双层视差与适配窗口的截图逻辑；两者保留原 GUID。新增背景接线/验证脚本放在 `Editor/Setup`，背景文档与历史验证记录分别归入 `Docs/Art` 与 `Docs/Validation`。目录审计对这两个已登记扩展检查内容变化与 GUID，其他迁移脚本继续检查源码内容。
