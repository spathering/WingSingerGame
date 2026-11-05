# 树木前景序列（2026-11-03）

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
