# 第 1 关：双循环与视频流程（2026-11-13）

范围：B 组需求。第 1 关为开场角色视频→镜头拉近→50 秒循环 1→完整中场视频→50 秒循环 2→原有结算。第 2 关保持原有 60 秒、上轨 4 秒/下轨 6 秒的流程；出镜箭头两关可用。正常关卡仍在 BreathDemo 中通过原有 Prefab 根对象 SetActive 切换，不重建或加载关卡场景。

## 视频与镜头

- `Assets/Art/LevelSequences/FirstFlightOpening.mp4`：原 a9af… 文件，1280×1280、24 fps、约 10.1 秒。开场玩家根对象停在出生点中轨，播放角色视频；专用 URP 透明材质去掉视频黑底。完整播放后，用 1.8 秒平滑拉近到现有正常镜头。
- `FirstFlightIntermission.mp4`：原 b6f8… 文件，1112×834、30 fps、实际约 52.38 秒，用户所称的“50 秒视频”完整播放，不截短、不变速。全屏画布等比适配，空余区域黑色，首尾各 0.8 秒淡入淡出，音频同步淡入淡出。
- 视频用 Unity VideoPlayer 和可复用 RenderTexture，开场时预准备两个片段；等待首帧后展示，过场时间不当作游玩时间。暂停显式暂停 VideoPlayer，退出停止并恢复角色展示，销毁时释放运行时纹理/材质。
- 专用 `FirstFlightSequence` 负责展示阶段，Cinemachine 的 `BreathCameraFraming` 在 Finalize 阶段应用远近镜头插值。其余时候保留原有跟随/取景约束。

## 两个游玩循环

关卡配置：`Assets/Settings/Levels/Level_001_FirstFlight.asset`。`inhaleSeconds=2`、`exhaleSeconds=3`、`durationSeconds=100`、`loopCount=2`、`loopSeconds=50`、`preparationSeconds=3`、`middleTailSeconds=2`、`playerEntranceSeconds=0`；视频接替本关原左侧飞入。

每个循环先中轨 3 秒，用原 0.65 秒换轨进入上轨；完成七组上轨 2 秒、下轨 3 秒及其换轨。最后从下轨平滑回中轨，保持到本循环 50 秒结束。中轨段由管理器强制中轨输入，玩家可放松；中轨不生成星星。两循环之间路线停在 50 秒，分数保留；只有到 100 秒才暴露 Finished，让现有 UI 正常结算。

纸鹤、星星、避让提示与计分都使用同一分段路线。第一循环末纸鹤先行时间钳制在 50 秒，等待玩家到达与休息结束；不提前显示第二循环的星星。路线计时在开场冻结；拉近镜头时建立纸鹤先行时间，拉近完成立刻开始玩家正常移动。

## 出镜箭头

`GuideOffscreenArrow` 位于关卡根对象，执行顺序在最终镜头之后。检测真实纸鹤 Transform 的视口坐标；离开屏幕时，在安全区域边缘显示金色箭头并朝向纸鹤，回镜后隐藏。过场、暂停、结算和关卡关闭时隐藏；不写 A 组 UI 资源，不改变纸鹤路径或强制拉远镜头。

## A 组对接与验证入口

实际状态/事件、时间与暂停语义见 `Docs/Handoffs/ParallelGameplay.md`。A 的 UI 文件未被本轮修改。音量和新增阶段文案的完整设置绑定留给 A；硬件协议、输入阈值不改动。

定向接线菜单：`WingSinger > B Group > Apply First Flight Sequence`。只更新 B 拥有的关卡资源及第 1 关配置；不重建 UI，不运行历史整批美术 setup。开发用本地请求 `Temp/WingSingerB.request` 支持 state/setup/validate/validate-full，回应在 `Temp/WingSingerB.response.txt`；validate 检查真实视频和边界，validate-full 额外实际运行两个完整游玩段。同一个编辑器仍需按并行约定排队。

验证报告：`Docs/Validation/FirstFlightSequence.txt`；开场、镜头过渡、中场截图：`Docs/Images/BSequence/`。验证应覆盖完整视频解码、暂停/继续、实际中场首尾淡变、两循环首尾中轨、旧 UI 只结算一次、重开/退出资源清理、第 2 关保持原配置，以及纸鹤上下出镜的箭头方向。检查不依赖真实硬件，不能当作硬件验收。

实现依据：[Unity 2022.3 VideoPlayer](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Video.VideoPlayer.html)、[VideoPlayer.Pause](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Video.VideoPlayer.Pause.html)。

2026-11-13 验收结果：常规验证和完整游玩验证均通过，最终报告为 SUCCESS。实际运行两段 50 秒游戏并收集星星，验证中轨强制放松、两段分数累积、中场暂停与全片淡变、最终结算、重开/退出及第 2 关保持原配置。过程中使用模拟输入，不冒充硬件连接验收。

上轮验证曾因系统内存不足中断；本轮在同一项目/缓存下，以单个后台导入进程启动编辑器后，Prefab 导入与全部验证成功。未调整永久项目设置。后台进程控制参考 [AssetDatabase.DesiredWorkerCount](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AssetDatabase.DesiredWorkerCount.html)。
