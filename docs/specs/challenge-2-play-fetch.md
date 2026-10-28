# Challenge 2 · Play Fetch

来源：Unity Learn "Create with Code" / Unit 2 - Basic Gameplay
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 4P「5月14日 Challenge2」
工程目录：`tasks/01-challenge-2-play-fetch/`
官方题目：[Challenge 2 - Play Fetch](https://learn.unity.com/course/create-with-code/unit/unit-2-basic-gameplay/tutorial/challenge-2-play-fetch?version=2021.3)

## 目标

狗接住从天上掉落的球。

## 达成条件

- [x] 屏幕上随机生成三种子球之一，位置在屏幕上方，水平坐标随机
- [x] 按下空格键生成一只狗，狗朝球跑去
- [x] 狗与球碰撞时销毁球
- [x] 球落到地面时在控制台输出 `Game Over`
- [x] 狗和球离开屏幕边界时被销毁

## 起始文件中的缺陷（逐条修复）

- [x] 球生成的高度不对
- [x] 生成出来的是绿色球，不是狗
- [x] 球只是靠近狗就被销毁（碰撞体与触发器设置错误）
- [x] 离开屏幕不会被销毁（`DestroyOutOfBounds` 的边界变量与比较符写错）
- [x] 永远只生成第 1 种球（需用随机整数索引去取 `Instantiate` 的对象）

## 加分项

- [x] 生成间隔在 3~5 秒之间随机（`InvokeRepeating` 做不到，需要 `Invoke` 加自调度）
- [x] 加冷却，防止连按空格无限刷狗

## 修复记录

### 场景接线：`Challenge 2.unity`

官方起始文件里，`Player` 上的 `dogPrefab` 指向的是 **Ball 2**（绿色球），而
`Spawn Manager` 的 `ballPrefabs` 三个槽位**全都指向狗的预制体**。所以玩家放出的是
一颗绿球，而天上掉下来的全是狗。

修复：`dogPrefab` 改为指向狗预制体的**根** GameObject，`ballPrefabs` 依次指向
Ball 1 / 2 / 3。

> 这里有一个容易踩的坑：Unity 在场景里存的是 `{fileID, guid}`。`guid` 指向预制体资源，
> `fileID` 指向**该预制体文件内部的那个对象**。只改 `guid` 而沿用原来的 `fileID`，
> 引用到的是预制体里的一个子节点（`GoatRig_Tail_01`），而不是根节点——表现是"引用上了，
> 但生成出来的东西没有标签、没有碰撞体、也不会动"。狗预制体的根对象 fileID 是
> `8241448880393145375`，名字 `Dog`，标签 `Dog`。

### `DestroyOutOfBoundsX.cs`

官方版本：`leftLimit = 30` 却判断 `x > leftLimit`；球用 `transform.position.z` 与
`bottomLimit` 比较——但场景里球是沿 **Y 轴**下落的，Z 恒为 0，所以球永远不会被销毁。

修复：狗与球分开判断，用标签区分：

- 狗（`CompareTag("Dog")`）：`x < -25` 时销毁，沿 X 轴向左出界；
- 球：`y < -5` 时销毁并输出 `Game Over!`。

### `DetectCollisionsX.cs`

官方版本只要碰到任何东西就销毁球。修复为只有碰到带 `Dog` 标签的碰撞体才销毁。

### `SpawnManagerX.cs`

官方版本用 `InvokeRepeating`（间隔固定）并且永远取 `ballPrefabs[0]`。修复为：

- `Random.Range(0, ballPrefabs.Length)` 随机取球；
- 用 `Invoke` 自己排下一次，间隔 `Random.Range(3, 5)` 秒。

### `PlayerControllerX.cs`

抽出 `SpawnDog()`，把冷却时间（0.5 秒）放在里面，空格键只负责调用。

### `Dog.prefab` 的碰撞体

官方碰撞体的**世界尺寸**约 2.32 × 38.76 × 78.09，而狗模型实测约 3.39 × 4.87 × 6.00：
长了约 13 倍、高了约 8 倍。这就是"球一靠近狗就被销毁"的真凶。

用 `Assets/Editor/MeasureTools.cs` 实例化预制体、取所有渲染器的合并包围盒，
在**预制体根节点的局部空间**里测得模型尺寸 `(0.17, 0.24, 0.30)`，
据此把 `BoxCollider` 的 `m_Size` 改为 `(0.17, 0.24, 0.3)`、`m_Center` 改为 `(0, 0.12, 0.04)`。

> 局部空间不能直接拿世界 AABB 换算：狗的根节点绕 Y 转了 -90°，直接换算会得到负的 size。
> 要把渲染器包围盒的 8 个角点逐个变换到局部空间再求 min/max。

### `MoveForwardX.cs` 没有改

`transform.Translate(v)` 的 `space` 参数**默认是 `Space.Self`**，狗的根节点又绕 Y 转了
-90°，所以脚本里的 `Vector3.forward` 实际是世界 **-X**，狗本来就是向左跑的。相机
（rotation 为 0，看向世界 +Z）让屏幕水平方向对应世界 X 轴。

## 验证

三层缝，见 `docs/agents/unity-workflow.md`：

| 层 | 工具 | 结果 |
| --- | --- | --- |
| 缝一 静态接线 | `tools/check-wiring.ps1` | 14 项全通过 |
| 缝二 Play 探针 | `Assets/Editor/Challenge2Probe.cs` | 15 项全通过（`checks: 15  failed: 0`） |
| 缝三 构建冒烟 | `tools/build-project.ps1` | 由打包阶段统一执行 |

探针断言的是外部可观察行为：三种子球随机出现、间隔在 3~5 秒之间、球在接触狗的瞬间
消失、球落到画面下方时控制台出现 `Game Over`、狗向左跑并出界销毁、狗的碰撞体与实测
模型一致、靠近狗的球不会被误杀。

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
