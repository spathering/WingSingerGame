# 两层循环视差背景

2026-10-24：BreathDemo 使用用户提供的 `背景.png` 和 `前景.png`，替换原来单张森林长图的显示。原森林素材保留供回退，游戏中只显示新的两层。

- `Looping Background`：远景，默认 `Parallax Speed = 0.65`，铺满视口。
- `Looping Foreground`：透明森林前景，默认 `Parallax Speed = 1.15`，位于画面底部，占屏幕高度 55%。
- 两层都位于共享场景根目录，不属于关卡 Prefab，切关和重开不会销毁重建。
- 前景叠在远景之上，角色、引导与收集物保持在两层之前，避免树木挡住操作内容。

速度相对于摄像机横向位移：摄像机前进 1 个世界单位，远景图案移动 0.65 个单位，前景移动 1.15 个单位。角色入场时已有连续水平移动，视差也随之开始；游戏停止前进时两层停止滚动。

## 配置

选择对应对象的 `LoopingPanorama` 组件：

- `Parallax Speed`：滚动倍率，0 静止，1 随世界位移正常滚动；越大越快。
- `Height Fraction`：占视口高度比例。远景默认 1，前景默认 0.55。
- `Vertical Anchor`：-1 底部、0 中间、1 顶部。
- `Source Aspect Ratio`：原图宽高比，保持原画比例，不将完整长图压进一个屏幕。
- `Parallax Anchor X` 和 `Origin World X`：初始镜头位置与图片左端基准，默认覆盖玩家入场前的镜头位置。

每层根据镜头的绝对世界位置采样 UV，不使用帧数或累积位移。因此重开、瞬移、切关后会恢复对应位置；横向 Repeat 循环，纵向 Clamp。

## 资源与渲染

- 远景原图 30910×1568，复制至 `Assets/Art/Background/SkyPanorama.png`。
- 前景原图 30910×1541，复制至 `Assets/Art/Background/ForestForeground.png`，保留原透明通道。
- 原始 PNG 完整复制，未改变像素。Unity 导入最大尺寸 16384，在桌面使用 BC7 压缩、关闭 Mipmap，不启用读写；运行时保留原图宽高比。
- 使用 `WingSinger/URP Parallax Panorama` 透明无光照 Shader，不修改 URP 档位、Renderer Feature 或 Volume。导入后的尺寸受 GPU 纹理限制，原始文件仍保留完整分辨率。

已保存场景可通过 `WingSinger > Apply Two Layer Background` 重复应用配置。此操作更新两层参数至上述默认值，需要先保存场景且退出 Play Mode。原来的 `Apply Forest Art` 在检测到两张新素材时也会重新应用两层背景，避免恢复成单层。

## 验证

`WingSinger > Validate Two Layer Background` 在已保存的主场景中检查合并后的导航与双层背景，不重新配置场景。测试使用隔离存档和 ExternalBreathInput，不打开物理串口，通过正式导航起飞、重开和进入第二关，再验证退出清理。输出 `Docs/Validation/ParallaxBackgroundMergeValidation.txt` 和 `Docs/Images/ParallaxBackground.png`。`ParallaxBackgroundSetup.ApplyAndValidateBatch` 另用于先应用默认背景参数再验证。

上游在 2026-10-24 使用 Unity 2022.3.62f2 通过 12 项运行检查，历史记录见 [ParallaxBackgroundValidation.txt](../Validation/ParallaxBackgroundValidation.txt)。2026-10-26 的合并验证单独记录在 [ParallaxBackgroundMergeValidation.txt](../Validation/ParallaxBackgroundMergeValidation.txt)，画面见 [ParallaxBackground.png](../Images/ParallaxBackground.png)。两张资源保持上游 LFS 内容；截图同步所有背景层，按实际渲染目标调整 Canvas，并在完成后恢复相机和 UI 配置。验证复用现有 Library，不清缓存或全量重导入。
