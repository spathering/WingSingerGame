# 2026-11-02 背景与 UI 动画替换

2026-11-03 更新：UI 以每渲染帧插值显示，不再只按 20 FPS 跳换 Sprite；原 100 帧/五秒时间轴保持。详见 [AnimationInterpolation.md](AnimationInterpolation.md)。

来源：`H:/PolyU/PM/WindSinger素材/11.2/背景.png` 与 `WindSinger_UI_IP_GIF_PNG`。原素材不修改。

## 背景

32768×1443 的新长图复制到现有 `Assets/Art/Background/SkyPanorama.png`，保留原资源 GUID、材质和场景引用。循环背景按新原图宽高比显示，继续横向 Repeat、纵向 Clamp；远景速度 0.65，原透明前景及其 1.15 倍速度保持。桌面导入沿用 16384 最大尺寸和 BC7，不在一个屏幕里压缩展示整张长图。

菜单页使用同一背景纹理的 Sprite，通过现有 `UIPageViewport` 等比铺满窗口并裁切显示其中一段。更新所有页面 Prefab、正式 BreathDemo 和 UIFlowPreview 的背景引用；正式游戏中的 HUD 背景显隐仍由原流程控制。

## UI 动画

源目录提供五组透明 PNG 序列，每组 50 帧。配套 GIF 中重复帧被合并，但总时长均为五秒。按后续要求，UI 与主角统一为 20 FPS：保留原 50 帧，在相邻帧之间（包括循环首尾）以预乘 Alpha 混合补入过渡帧，输出 100 帧，每帧 50ms，循环仍为五秒。补帧改善视觉连续性，不代表原素材提供了 100 个独立动作姿态。

- `01_Level_Select` → 选关页面的引导飞艇。
- `02_Ready_for_Takeoff` → 起飞准备页面的飞艇。
- `03_Pause` → 暂停页面的角色。
- `04_Flight_Complete` → 完成页面的庆祝飞艇。
- `05_Flight_Log` → 飞行记录页面的角色。
- 首页的 HomeClean、HomeDashboard 两种版本 → 共用 `02_Ready_for_Takeoff` 动画资源，与起飞准备页保持一致。

使用 `Tools/UI/import_november_art.py` 将各序列打包为透明图集，帧宽 384，保持长宽比及统一画布、两像素间隔，图集不超过 8192。补帧后提高 UI 图集导入上限，以保留原帧分辨率；主角及其他既有图集仍采用原有导入上限。素材位于 `Assets/Art/UI/Animation`，Manifest 记录源序列与补间算法的哈希、尺寸及帧时长。运行时复用 `CharacterSpriteClip`，通过 `UISpriteAnimation` 更新 uGUI Image，不依赖 Unity 直接解码 GIF。

动画使用 unscaledDeltaTime，游戏暂停时仍然播放；页面隐藏时停止 Update 并回到首帧，再打开重新播放。图像保留透明通道和等比显示，关闭 raycastTarget，避免遮挡按钮点击。选关页的新完整飞艇放到卡片左侧的可见区域，避免沿用旧插画出屏位置裁掉角色。

## 应用与验证

素材打包后，运行菜单 `WingSinger > UI > Apply November Artwork`。该操作仅用于已保存、非 Play 状态；不重建 UI 布局，不更改按钮事件、硬件配置和关卡管理。重新生成 UI 页面后可再次运行此菜单恢复新插画。

批处理入口：`WingSinger.Editor.NovemberArtSetup.ApplyAndValidateBatch`，复用当前工程 Library 和增量编译；已打开编辑器可通过现有本地桥接请求 `apply-november-art` 运行同样检查。验证不连接实际硬件、不写用户存档：检查七页动画、主界面共用起飞资源、20 FPS 时序、暂停播放、隐藏复位、新背景比例与循环、切关/重开复用关卡实例；分别输出 1280×720、1024×768、2560×1080 页面预览及游戏截图。后台运行设置仅在验证期间临时启用，结束后恢复。

验证结果见 `Docs/Validation/NovemberArt.txt`，截图见 `Docs/Images/NovemberArt/`。保留原 URP、Renderer、Volume、程序集及包依赖。
