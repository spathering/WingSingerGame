# 纸制收集物与离散计分

2026-10-22：示例场景仍为 `Assets/Scenes/BreathDemo.unity`，继续通过同场景关卡 Prefab 根对象的激活状态切换关卡。

## 素材与表现

- 前方引导使用 `craneFlat (1).gif` 纸鹤，沿原有平滑路线移动。
- 可收集物使用 `star.gif` 纸星星。已按最新确认对调素材用途。
- 两个 GIF 均为 500×500、120 帧、每帧 20 毫秒，循环时长 2.4 秒。转换保留透明通道，运行时使用 `Assets/Art/Paper` 下的 PNG 图集及 Sprite 子资源，不依赖项目外文件。
- 默认隐藏连续发光线，以离散收集物显示路线；保持无顶部、底部或结算 UI 的游戏画面。
- 碰到收集物后，每颗只加一次分，收集物短暂发亮并缩小消失，角色周围出现短暂收集闪光。

## 布点规则

布点由 `BreathCollectibleLayout` 根据关卡时长及呼吸节奏生成，横坐标为路线时间乘移动速度，纵坐标始终为上、中、下三条轨道之一。

1. 准备段中轨、吸气段上轨和呼气段下轨均为平直段。每段首尾各有一颗，中间均匀排列。
2. `collectibleIntervalSeconds` 是平直段的最大时间间隔。若段长不能被间隔整除，使用 `ceil(段长 / 间隔)` 个间隔均分本段，保证间距一致并包含首尾；相邻段实际间隔可因段长略有不同。
3. 上轨到下轨、下轨到上轨的平滑曲线，在曲线时间中点经过中轨，交点必有一颗。曲线其他位置不放收集物。
4. 开局路线起点在中轨，必有一颗；准备时间为 0 时不会重复生成同一颗。初次中轨到上轨只保留轨道端点，不在半轨高度放置收集物。
5. 游戏时长截断平直段时，截断点作为该段末端加入。结束落在曲线中途时，仅保留结束之前的合法轨道交点，不在轨道之间放置收集物。
6. 收集物随引导经过相应位置显现，位置固定，不随引导一起移动。引导到达路线末端后等待玩家完成余下距离。

## 计分与判定

分数为 `CollectedCount × pointsPerCollectible`。沿路线停留或经过曲线本身不会自动加分。预览阶段不允许收集；玩家开始移动后，准备段中轨和换轨时的中轨收集物均可计分。

判定使用角色身体前后两帧的实际世界位置做二维扫掠距离检查，而非仅检查按键或目标轨道。这使玩家返回中轨、上下直接换轨和较长帧经过收集物时仍能触发接触。只有进入 `collectionRadius` 范围才会收集，漏过的物体不加分。最后一个有效移动帧仍可收集，之后的结束帧关闭收集窗口。

`BreathSession` 记录已收集的 ID，重复接触不重复计分。原有路线匹配时长仅保留为调试统计，不参与分数。

## 关卡配置

在 `Assets/Settings/Levels` 的两个 `BreathLevelSettings` 中调整：

- `Points Per Collectible`：每颗分值，默认 10。由旧 `pointsPerSecond` 字段迁移数值，现含义改为每颗计分。
- `Collectible Interval Seconds`：平直段最大生成间隔，默认 0.5 秒，最小 0.05 秒。
- `Collection Radius`：身体与收集物之间的接触半径，默认 0.55 世界单位。
- `Show Guide Line`：是否显示原连续路线，默认关闭，仅供需要时辅助观察。

吸气、呼气、准备时间、轨道间距及移动速度仍使用现有配置。`Guide Clip / Guide Visual / Collectible Prefab` 在关卡 Prefab 上绑定；共用收集物模板位于 `Assets/Prefabs/Gameplay/PaperCollectible.prefab`。

## 生命周期与实现

每个关卡首次进入时建立收集物池，重开复用原有对象，配置需要更多物体时才扩展。退出时关闭所有收集物并停止 PrimeTween 收集效果；重新进入时重置收集、漏过和显隐状态。运行过程中不销毁或重新创建整个关卡，两个关卡的物体池独立。

`BreathLevelManager.LateUpdate` 在位置补间更新后检查实际接触；`BreathLevel` 负责显现、收集和池复用；`PaperCollectible` 负责 GIF 帧和收集表现。没有新增物理刚体、Addressables 加载流程或第三方插件。材质复用项目内的 URP 无光照透明 Sprite Shader。

## 转换与验证

在项目根目录运行，需要 Python 与 Pillow：

```powershell
python Tools/convert_paper_gifs.py 'H:\PolyU\PM\WindSinger素材'
dotnet run --project Tools/CollectionLogicTests/CollectionLogicTests.csproj -c Release
```

导入后可在已保存的示例场景执行 `WingSinger > Apply Paper Collection Art`，更新素材绑定而不重建场景和关卡配置。完整 Unity 验证入口为 `WingSinger.Editor.PaperCollectionSetup.ApplyAndValidateBatch`，使用当前项目缓存完成资源应用、运行检查和截图。

逻辑测试覆盖平直段均匀性、首尾、中轨交点、时长截断、去重、离散计分、预览、最后一帧和重置。运行测试另覆盖实际移动收集、错轨不收集、扫掠检测、GIF 帧、对象池复用和切关清理。

本次已通过 151 项独立逻辑断言及 Unity 2022.3.62f2 Play Mode 验证，记录见 [PaperCollectionValidation.txt](../Validation/PaperCollectionValidation.txt)，截图见 [PaperCollection.png](../Images/PaperCollection.png)。沿用已有 Library 缓存，只增量编译相关 Main / Editor 程序集，没有清缓存或进行全量重建。

后续确认已将两种素材用途对调：纸鹤负责引导，纸星星负责收集；旧截图保留为初版展示，当前素材对应以上文与 Prefab 绑定为准。
