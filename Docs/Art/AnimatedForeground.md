# 树木前景序列（2026-11-03）

## 2026-11-05：关卡前景与树木尺寸

- `BreathDemo` 的 `Looping Foreground.sizeMultiplier` 设为 `2`。相对于原 `heightFraction = 0.55`，树木的显示高度和图像循环宽度均为原来的两倍，1.15 倍视差速度和动画序列保持原逻辑；`verticalOffsetFraction = -0.15` 将中景在原底部锚点基础上累计下移自身显示高度的 15%（本次在上一版基础上上移 5%），随视口尺寸等比计算。Quad 宽度继续覆盖视口；图像宽度通过 UV 周期变化实现，不能直接把 Quad 的 Transform 放大来替代。
- `Assets/Art/UI/Frontgrounds` 的 18 种透明素材全部部署到默认第一关 `Level_001_FirstFlight.prefab`，位于其 `Foreground Decorations` 子节点。沿完整路线交错排列，按 Alpha 内容区域统一对齐地面，避免 2048×2048 透明留白影响尺寸和落点。使用现有 `BreathSprites` URP 材质，排序为 -5，在树木层 -10 之上、玩法对象之下，不附加碰撞器。
- 新装饰由关卡内的 `LevelForegroundParallax` 按相机绝对位移移动，速度为相机的 1.15 倍，与中景树木的 1.15 倍一致。组件在 Cinemachine 之后更新，通过平移装饰根节点实现差速，没有逐帧累积误差、独立动画、音效、订阅或异步任务。由 `BreathLevel.Prepare/Exit` 显式复位、统一管理器在设置新会话及相机位置后绑定；暂停冻结，关卡关闭时清除引用。重开和返回关卡复用同一批实例。其他关卡未添加这批装饰；树木继续复用共享动画层。
- 每个物件缩小到上一版放大尺寸的 50%（相对首次部署尺寸为 1～1.5 倍），保留随机大小差异，真实可见边缘之间随机留 2.5～8.5 世界单位间隔，底部少量沉入屏幕外，强化近景效果。颜色使用 54%～64% 的偏冷暗色调，保留 Alpha，不改变源素材和共享材质。编辑器使用独立随机数种子 1105 生成并保存布局，运行时不随机重排；布置长度按 1.15 倍移动速度覆盖完整飞行路线。
- 可通过 `WingSinger > Apply Level Foreground Decorations` 重新部署到当前主场景配置的默认关卡；需退出 Play 并保存场景，重新部署只替换该关卡的 `Foreground Decorations` 子节点。`Apply Animated Foreground` 同样保留树木的 2 倍设置。
- 在现有 Unity 2022.3.62f2 编辑器中完成增量编译和 Play 验证：18 种素材引用、URP 材质、树木 2 倍宽高与累计下移自身高度 15%、物件缩小 50%、随机间隔、调暗、全程覆盖、实际相对移动速度、暂停、重开、切关关闭、返回复用及退出复位。报告见 `Docs/Validation/LevelForeground.txt`，路线预览为 `Docs/Images/LevelForeground_0.png` 至 `LevelForeground_2.png`。验证未连接硬件或写入玩家进度。

后续流畅度/白边更新：现在按每个渲染帧对相邻源帧插值，20 FPS 仅描述源时间轴；半透明偏白边缘已修复。PNG 大小更新为 224,065,763 字节，以下 217 MB 为处理前记录。原理和验证见 [AnimationInterpolation.md](AnimationInterpolation.md)。

来源：`H:/PolyU/PM/WindSinger素材/11.2/hc1/hc1/`，150 张 RGBA PNG，单帧 30000×1562。源文件合计 6,145,916,617 字节，原目录不改动。

## 压缩与资源

`Tools/import_foreground_sequence.py` 保留完整画布、全部 150 帧和 Alpha，将单帧内容等比缩至 4096×213；底部补 3 行透明像素到 4096×216，以满足 BC7 的四像素块对齐，渲染 UV 排除这三行。输出目录为 `Assets/Art/Background/AnimatedForeground`；Manifest 记录原始尺寸、顺序、源文件 SHA256、压缩前后字节数及播放帧率。

输出 PNG 合计 217,410,130 字节，约为原大小的 3.54%（减少约 96.46%）。没有将 6 GB 原始图片另存进 Unity 工程。4096 是当前内存与细节的折中；由于原图非常宽，缩小后局部树木放大显示时精细度会低于原图。

Windows 桌面导入使用 BC7，横向 Repeat、纵向 Clamp，关闭 Mipmap 与 Read/Write；不保留运行时 CPU 像素副本。150 张压缩纹理理论合计约 127 MiB（含纹理边界按压缩块对齐），实际运行时统计见验证报告。PNG 磁盘大小、Library 导入缓存和显存用量是不同数据，不将 PNG 的 217 MB 当成显存数字。

本次 Unity Editor 的 `Profiler.GetRuntimeMemorySizeLong` 对全部纹理合计报告 265,522,800 字节，约 253 MiB；这是编辑器运行时的纹理对象统计，不等同于仅计算 BC7 块的纯 GPU 数据大小。150 帧全部常驻，避免播放时磁盘读取或解码卡顿；本轮没有引入视频插件或逐帧异步加载。

## 场景与播放

沿用共享场景对象 `Looping Foreground` 与现有 URP 材质/Shader，保持前景 1.15 倍、远景 0.65 倍差速及原有高度/排序。原图宽高比用于循环几何计算，避免因为纹理高度取整产生拉伸。旧静态前景保留为回退素材，正式材质以新序列首帧作为默认贴图。

`LoopingPanorama` 新增可选纹理序列，通过现有 MaterialPropertyBlock 同时设置 UV 与当前纹理；不逐帧创建材质、纹理或 Sprite。原静态背景不配置序列，继续按原逻辑显示。

源文件没有帧率元数据，默认按之前角色和 UI 的约定使用 20 FPS，保留全部帧，因此一轮 7.5 秒。可调整 `Animation Frames Per Second`。计时跟随正式游戏会话：菜单停在首帧，开始/切关/重开时复位，暂停时冻结，离开游戏时回到首帧。序列的空间循环继续由相机绝对位置控制，时间动画与空间位移互不替代。

## 应用与验证

菜单 `WingSinger > Apply Animated Foreground` 将当前主场景接到已导入序列，要求已保存且非 Play 状态。原 `Apply Two Layer Background` 在发现新 Manifest 后也会应用动画，避免重新接线时退回旧静态前景。

验证入口 `WingSinger.Editor.AnimatedForegroundSetup.ApplyAndValidateBatch`：复用工程缓存，检查帧数/分辨率/BC7/CPU 读写设置、实际渲染纹理、时序循环、暂停、重开、切关、退出和空间循环。不接真实硬件、不写用户存档。报告 `Docs/Validation/AnimatedForeground.txt`，预览 `Docs/Images/AnimatedForeground.png`。
