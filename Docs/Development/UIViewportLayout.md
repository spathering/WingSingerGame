# UI 窗口适配

## 问题与修正

原页面依据约 `2420×1668` 的参考图排版，Canvas 也以此为参考尺寸，并使用宽高各 50% 的缩放匹配。页面中 Header、Content、Footer 的子项大量使用固定坐标，16:9 窗口下可用的逻辑高度可能小于原稿高度，因此底部控件超出窗口。只改变 Canvas 参考尺寸无法同步修正这些固定坐标。

现在采用两层适配：

- Canvas 统一为 **Scale With Screen Size / 1920×1080 / Match 0.5**。
- 每个页面由 `UIPageViewport` 将原有 `2420×1668` 内容等比缩放、居中放入当前可用区域；保留原始控件位置和引用，避免图标与字体被非等比拉伸。
- 背景独立按 Cover 方式铺满窗口，可裁切背景边缘；按钮、文字和面板完整放入内容区域。
- 窗口改变时重新计算布局。屏幕存在 Safe Area 时，内容按安全区域适配，背景继续铺满窗口。

装饰性插画允许延伸到画面边缘；交互控件与文字不能超出可视区域。超宽和较方窗口会在内容两侧或上下展示更多背景，内容不拉伸。

`UIPageViewport` 使用 DrivenRectTransformTracker 管理页面根节点和背景的动态 RectTransform；Inspector 中相应字段显示为受布局组件驱动。布局在编辑器和运行时生效，独立 Prefab 编辑阶段保留参考稿坐标。不要移除组件后单独修改页面根节点缩放以适配某一个窗口。

## 入口与维护

- `WingSinger > UI > Apply Responsive Viewports`：更新 13 个页面、完整 UI 根 Prefab、BreathDemo 和 UIFlowPreview 的配置；保留已接入的业务绑定与页面美术修改。先保存场景和 Prefab Stage。
- `WingSinger > UI > Validate Responsive Viewports`：在实际 Play Mode 中使用不同尺寸的渲染目标检查全部页面，测试结束返回主场景。
- `Build Reference UI` 已包含相同配置，重建后不会恢复旧的 Canvas 参考尺寸。
- 原稿、原有参考截图仍以原设计比例保存；16:9 和其他比例的运行证据独立放在 [ViewportPreviews](../Design/UI/ViewportPreviews/)。

## 验证范围

全部 13 页检查以下窗口尺寸：1920×1080、1280×720、2560×1440、1600×900、1920×1200、1024×768、2560×1080、800×600、640×360。

验证读取实际 RectTransform 世界角点、TMP 渲染文字边界，并映射到渲染目标像素，检查页面内容、按钮/滑条/开关、文字和背景覆盖；另检查缩放后的 GraphicRaycaster 命中及悬停动画边界。测试同时复现旧参数的 16:9 越界，防止只检查配置值而漏掉实际显示问题。

结果见 [UIViewportValidation.json](../Validation/UIViewportValidation.json)。完整玩法仍由 [GameplayNavigationValidation.json](../Validation/GameplayNavigationValidation.json) 回归，关卡管理、串口协议和 URP 配置不因本次适配改变。
