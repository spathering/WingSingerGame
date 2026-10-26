# 角色动画与森林循环背景

2026-10-24：当前示例场景已改用两层循环视差素材，远景 0.65 倍、前景 1.15 倍。资源、参数和验证见 [ParallaxBackground.md](ParallaxBackground.md)。下文单层森林背景部分保留为早期实现说明。

示例场景：`Assets/Scenes/BreathDemo.unity`。美术与共享玩家、摄像机放在关卡 Prefab 之外；关卡仍由 `BreathLevelManager` 在同一场景内切换根对象激活状态。

## GIF 动画

原始素材位于用户提供的 `WindSinger素材/IP_Six_GIFs`。Unity 运行时使用预先转换的 PNG 图集和 `CharacterSpriteClip` 资源，不需要 GIF 解码插件或外部绝对路径。

- `01_Cruise_5s`：抵达任意轨道、完成收姿后的平飞，也用于入场、准备与结束状态。
- `02_Rise_5s`：实际向上跨轨移动期间播放，包括从下轨返回中轨。
- `03_Descend_5s`：实际向下跨轨移动期间播放，包括从上轨返回中轨。
- `04_Cruise_to_Descend`、`05_Cruise_to_Rise`：在下降、上升结束后倒序采样，从倾斜姿态平滑收回平飞。
- `06_Complete_Loop_9_5s`：仅保留资源供预览参考，不绑定游戏或结算状态。

前三个循环保留原始帧时长（每帧 50 毫秒、各 100 帧）。角色在跨轨移动时选择上升或下降动画，抵达轨道后先播放对应的收姿过渡，再恢复平飞；即使持续按住 W 或 S 停留在上、下轨，也播放平飞。动画由实际移动方向决定，中途换向立即切换，不等待旧循环或收姿过渡播完。`BreathCharacterAnimator.returnToCruiseSeconds` 控制收姿时长，默认 0.28 秒；原素材不改帧、不新增图片，运行时按进度倒序播放 04/05。

关卡配置中的 `laneTransitionSeconds` 控制角色实际跨轨移动时间，也决定上升或下降动画的播放区间。PrimeTween 更新身体位置后，`BreathPlayer.LateUpdate` 根据目标位置与补间状态刷新角色动画。转换保留 GIF 自带的透明通道，图集隔离边缘也保持透明；专用 URP Shader 可过滤残留的高饱和洋红色，不改变角色原画。旧几何角色部件已关闭，仅保留用于得分反馈的 Aura。

图集每帧宽 256 像素、高度依原始比例计算，四周留 2 像素隔离边缘。`CharacterManifest.json` 保存原始文件名、SHA-256、布局和每帧时长；`.asset` 保存 Sprite 子资源，构建后也可直接播放。运行时引用前五个动画，第六个完整循环仅供预览参考。

重新转换（在项目根目录运行，需要 Pillow）：

```powershell
python Tools/convert_character_gifs.py 'H:\PolyU\PM\WindSinger素材\IP_Six_GIFs'
```

导入变化后，在已打开并保存的 BreathDemo 场景执行菜单 `WingSinger > Apply Forest Art`。该操作更新现有场景和对应资源，不重建关卡配置，也不改变 Prefab GUID。修改转换器的分辨率或布局后，需要重新生成对应 `.asset`，不能只换 PNG 而保留旧的 Sprite 矩形。

## 循环背景

原图 `微信图片_20261017164503_128_313.png` 已完整复制为 `Assets/Art/Background/ForestPanorama.png`，保留 8192 × 902 像素。纹理横向 Repeat、纵向 Clamp，关闭 Mipmap 与缩放；背景采用无光照 URP 材质，保持原图颜色和纵横比。

场景中的 `Looping Background` 是公共背景对象。`LoopingPanorama` 在 Cinemachine 更新之后，根据正交摄像机的世界 X 计算纹理采样区间；摄像机向右移动会依次显示长图右侧内容，到达图尾后接回图头。整张图的世界宽度由视口高度和图片比例决定，默认约 104 个世界单位。即使一次跳过多个循环或重开跳回出生点，也使用绝对位置恢复正确区间。

背景始终使用同一个网格，不在游玩过程中创建、销毁或累积背景对象。改变窗口比例时只改变可见宽度；不会将整张长图挤进屏幕。`originWorldX` 为图片左端对应的世界位置，`depth` 为背景深度。背景对象不要放入可关闭的关卡 Prefab。

两个示例关卡旧的 Star / Nebula 装饰和三条固定 Track 线已移除。2026-10-22 起，引导使用 craneFlat (1).gif 纸鹤，连续路径默认隐藏，由 star.gif 纸星星显示路线并逐颗计分，详见 [PaperCollection.md](../Design/PaperCollection.md)。示例场景保持无 HUD 与轨道文字，R 重开、N 切换下一关。共享 Volume 的 Bloom 与 Vignette 已减弱，适配明亮森林；未新增渲染管线或第三方依赖。

后续验证需覆盖平飞、上升、下降、按键中途反向、重开、两关切换、循环接缝与不同窗口比例。素材替换只需针对变化资源导入；遵守开发指南中的增量编译流程。

2026-10-20 已在当前项目的 Unity 2022.3.62f2 编辑器完成运行验证与画面检查，记录见 [ArtValidationReport.txt](../Validation/ArtValidationReport.txt)，截图见 [ForestFlight.png](../Images/ForestFlight.png)。本次复用了现有 Library 缓存，仅编译相关程序集并定向导入变化资源。

移除 UI、固定轨道线与改为移动方向动画后的补充验证见 [CleanPresentationValidation.txt](../Validation/CleanPresentationValidation.txt)。覆盖上/下轨停留恢复平飞、返回中轨、上下轨直接互换，以及无 HUD 时的重开、切关和结束。

## 玩家入场与收姿（2026-10-22）

每次开始、重开或切关，玩家在中轨从视口左边界之外飞入，默认 1.25 秒，逐渐收拢至正常跟随位置。入场起点根据当前正交视口宽度和角色实际渲染边界计算，兼容不同画面比例。角色根对象从路线起点左侧开始，预览期间即以正常速度向右移动，Cinemachine 和森林背景同步滚动；身体的局部 X 逐渐回到 0。入场末端的局部速度归零，世界速度保持正常前进速度，不再停住等待预览结束。入场与引导先行预览重叠，不占正式时长、不计分；预览时暂不接受换轨输入。

`BreathLevelSettings.playerEntranceSeconds` 可逐关配置，0 表示关闭入场。实际预览时长取该值与 `guideLeadSeconds` 的较大者，确保正式路线计时与收集开始前已完成入场。接近路线期间不扣正式游戏时间；到达路线起点后连续进入正式游玩，运动和相机均不跳变。切关和重开会重置身体偏移、换轨补间、收姿进度和动画时钟。现有 PrimeTween 继续负责垂直换轨；入场和预览期间的水平位移由同一会话时间直接采样，避免独立补间时钟在重开或跳帧时不同步。

使用 `WingSinger > Validate Player Presentation` 在当前编辑器验证入场、双向收姿、中途反向、实际跨轨停留与重开切关，报告位于 `Logs/PlayerPresentationValidation.txt`。该菜单会先为已保存的 BreathDemo 绑定现有 04/05 动画，再进入 Play Mode；拒绝覆盖未保存的场景。

已在现有 Unity 2022.3.62f2 编辑器通过 29 项 Play Mode 检查，报告见 [PlayerPresentationValidation.txt](../Validation/PlayerPresentationValidation.txt)，入场截图见 [PlayerEntrance.png](../Images/PlayerEntrance.png)。纸星星纯逻辑测试的 151 项断言仍通过。复用现有缓存，运行时代码仅影响 Main 及其 Editor 依赖；验证脚本修正仅重新编译 Editor，未清缓存或全量重导入。

2026-10-22 衔接修正：回归检查额外覆盖入场结束前后的世界速度、正式计时开始前后的位移连续性、相机连续性和预览期间的实际滚动。背景的 `originWorldX` 同时前移一个预览行进距离，让入场从原图左端开始，避免开场提前显示循环接缝；应用森林美术时也会按初始关卡配置计算该偏移。验证运行时临时允许后台更新，结束后恢复原值，不修改 Player Settings。修改前先验证本地 Unity MCP 已连接 `WingSingerGame`；默认端口 8080 被 Steam 占用，当前编辑器 MCP 服务地址为 `http://127.0.0.1:8081/mcp`。
