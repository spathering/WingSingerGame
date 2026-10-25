# UI 素材与 Prefab 规范

## 来源与交付

参考来自用户指定的 `D:\Studies\ProjMgrt\wingsinger`，共 13 张 2420×1668 PNG。原始文件不修改；副本和 SHA-256 记录在 `Docs/Design/UI/References` 与 `Assets/Art/UI/UIAssetManifest.json`。

切图脚本：`Tools/UI/prepare_ui_assets.py`。图形按原图像素裁切，图标去除原先的纯色底板，角色按轮廓与天空/森林色区制作透明蒙版。原始矩形、处理方法、尺寸和哈希逐项保存在 manifest 中，可追溯到具体页面。

森林底图使用内置 image_gen 从练习 Follow 图补全，移除了文本、按钮、角色和路径，作为可复用背景板。生成指令保存在 `Tools/UI/BackgroundPrompt.txt`。它是根据参考补全的独立插画，不冒充参考图原始分层源文件。

## 资源目录

| 目录 | 内容与导入 |
| --- | --- |
| `Backgrounds` | ForestValley，独立完整森林背景 |
| `Sprites` | 品牌 Logo、角色、森林标识、控制器插图和透明图标 |
| `Illustrations` | 现有项目透明纸鹤/星星首帧、虚线引导路径与呼吸节奏曲线 |
| `Skins` | 面板、按钮正常/悬停/按下/禁用、滑轨、把手、开关与色块 |
| `Fonts` | Darumadrop One 原字体、OFL 许可和生成的 TMP SDF |

60 项可复用 PNG 全部以 Sprite/Single 导入；Full Rect、无 Mipmap、Clamp、Bilinear、透明纹理启用 Alpha Is Transparency，使用未压缩纹理避免 UI 小图边缘失真。面板和按钮具有 spriteBorder，uGUI Image 使用 Sliced；角色/图标使用 Simple + Preserve Aspect。滑条与进度条使用独立白色胶囊皮肤及 Pixels Per Unit Multiplier，保持细条圆端，不将大按钮挤成椭圆端帽；勾选图标提供绿色和白色透明变体。

原图中按钮的文字没有烘进通用按钮底板。正文、标题、数字和状态均为可编辑 TMP 文本；Logo 保留原品牌图形。字体来自 [Google Fonts 的 Darumadrop One](https://github.com/google/fonts/tree/main/ofl/darumadropone)，[OFL 许可](../../Assets/Art/UI/Fonts/OFL.txt) 随资产保存。

## 共用组件

`Assets/Prefabs/UI/Shared` 内包含 7 个组件：

| Prefab | 内容 |
| --- | --- |
| PrimaryButton | 橙色主按钮、可编辑 Label、四态 SpriteSwap、UIRouteButton |
| SecondaryButton | 奶油色描边次按钮、可编辑 Label、交互状态 |
| StatCard | 可更换底板、Icon、Label、Value |
| LevelCard | 森林缩略图、编号、状态图标；页面中按示例数据设置选中/锁定 |
| ConnectionBadge | 链接图标与连接状态示例文案 |
| ScreenHeader | Logo 与返回入口的标准排布样板 |
| PracticeHUD | 暂停、步骤、连接状态和星星数量的共用顶栏 |

页面以嵌套 Prefab 复用按钮、统计卡、关卡卡和练习顶栏。`UIFlowRoot.prefab` 提供 Canvas、CanvasScaler、GraphicRaycaster、13 个嵌套页面和 UIScreenRouter；每页内按 Background / Artwork / Header / Content / Footer 分组。

CanvasScaler 基准为 1920×1080，Match Width Or Height=0.5。页面保留 2420×1668 的原稿坐标，由 `UIPageViewport` 等比缩放并居中放入当前窗口和安全区；背景按 Cover 铺满窗口，角色、图标与字体不被非等比拉伸。页面不自带重复 EventSystem；预览场景有且只有一个 EventSystem。已验证 16:9、16:10、4:3 和超宽窗口，参见 [窗口适配说明](../Development/UIViewportLayout.md)。

## 重建与预览

1. 运行素材切分脚本（只有需要更新素材时才执行）。
2. 在当前 Unity 中等待脚本正常增量编译完成。
3. 执行 `WingSinger > UI > Build Reference UI`。
4. 打开 `Assets/Scenes/UI/UIFlowPreview.unity`。
5. 对照 `Docs/Design/UI/References` 与 `Previews` 检查。

构建器会重新生成其拥有的页面/共用 Prefab，定制页面建议建 Prefab Variant。它不会覆盖游戏场景、角色动画素材、渲染配置或关卡 Prefab。

验证范围包含真实 Unity 导入与序列化、脚本引用、Sprite 引用、可编辑 TMP、按钮射线命中、单 UI 激活不变量、导航链和实际 Canvas 离屏截图。报告见 [UIBuildValidation.json](../Validation/UIBuildValidation.json)。实际 Play Mode 的按钮监听、滑条、开关和返回链另见 [UIPlayValidation.json](../Validation/UIPlayValidation.json)。
