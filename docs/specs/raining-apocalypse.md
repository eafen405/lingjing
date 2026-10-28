# Raining Apocalypse（完整游戏）

来源：Udemy "Make Your First 2D Game with Unity & C# – Beginner Course"（Noa Calice / Blackthornprod）
对应视频：[Unity 2D 游戏开发入门：C# 零基础完整实战](https://www.bilibili.com/video/BV1xegN6DEv9) 第 27P 起「04-制作游戏」
工程目录：`tasks/05-raining-apocalypse/`

作者公开的工程：`https://github.com/Liam-Calice/MakeYourFirstGame`（原始工程版本为 Unity 2018.3.0f2）

## 目标

一款 2D 躲避游戏：玩家左右移动，天上不断落下障碍物，坚持存活。课程最终把它发布成 WebGL 版本到 itch.io。

## 达成条件

### 场景与流程

- [x] 两个场景：`MainMenu.unity` 与 `Game.unity`，都在 Build Settings 里
- [x] `GameManager.PlayGame()` 调用 `SceneManager.LoadScene("Game")` 进入游戏
- [x] 游戏结束显示 LosePanel，可 Restart

### 玩家

- [x] Rigidbody2D，方向键左右移动
- [x] `isRunning` 动画 bool 参数驱动 idle/run 过渡
- [x] 移动时精灵按方向翻转
- [x] `health` 数值与 `TakeDamage(int)` 方法
- [x] 血量在 UI 上显示，归零进入失败画面

### 障碍物

- [x] 三个障碍物预制体：`Hazard.prefab`、`Hazard 2.prefab`、`Hazard 3.prefab`
- [x] 速度在 `[min, max]` 区间随机，向下移动
- [x] 用 `OnTriggerEnter2D` 区分撞到 `Player` 与撞到 `Ground`

### 生成器

- [x] `Spawner.cs`：`Transform[] spawnPoints`、`GameObject[] hazards`
- [x] 生成间隔随时间递减（`startTimeBtwSpawns` → `minTimeBetweenSpawns`，每步 `decrease`）
- [x] 玩家对象消失时停止生成

### 美术与动画

- [x] 自己制作的 2D 美术，玩家切成 Head / Body / Arm Left / Arm Right / Leg Left / Leg Right
- [x] `Player.controller` 动画控制器，含 `idle.anim` 与 `run.anim`，靠 `isRunning` 过渡
- [x] 环境：`Ground`、`WallLeft`、`WallRight`、`Clouds` ×7、`Mountains` ×3

### 粒子

- [x] `Explosion.prefab` 粒子系统，颜色/大小/生命周期随机，碰撞时触发

### UI（UGUI）

- [x] `Canvas` + `EventSystem`
- [x] 血量 `Text`、`Image`、`Button`（`PlayButton`）
- [x] 主菜单含 Logo 与 Play 按钮
- [x] 失败画面含 Restart 按钮

### 音频

- [x] `Explosion01`~`Explosion04.wav`、`Game Over.wav`、`Hurt.mp3`、`Music.mp3`
- [x] `RandomSound.cs`：从数组中随机取一段播放
- [x] `MusicManager.cs`：`DontDestroyOnLoad` 单例

### 冲刺移动

- [x] `startDashTime`、`dashTime`、`extraSpeed`、`dashing` 标志位，配合 `Time.deltaTime`
- [x] 注意：作者公开的工程里**没有**这一项，需要自己实现

### 发布

- [ ] 导出 WebGL 构建并发布到 itch.io（课程第 37 节）
  - 本阶段不做：打包与发布由后续的打包票负责，本票不导出任何玩家构建。

## 课程额外内容

- [x] 「冷热游戏」控制台练习（随机 0~100，给出冷热反馈）
- [x] C# 基础部分的约 7 个编程练习

## 本票修复记录

一行一条，写清楚原来哪里不对、这次改了什么。

1. **冲刺移动整个缺失**：作者公开的工程里没有冲刺 —— `Player.cs` 新增 `startDashTime`、`dashTime`、`extraSpeed`、`dashing` 四个字段，空格键触发，冲刺时移动速度加上 `extraSpeed`，冲刺持续 `startDashTime` 秒，这段时间内不能再触发第二次冲刺。
2. **玩家死亡后新生成的障碍物会抛空引用**：`Enemy.Start()` 里的 `FindGameObjectWithTag("Player")` 可能返回 null —— 加了空判断，`OnTriggerEnter2D` 里的 `TakeDamage` 调用也加了保护。
3. **冲刺时精灵可能朝向错误**：冲刺方向原本只看冲刺开始那一刻的按键 —— 改成冲刺期间跟着当前按住的键走，松开时沿冲刺方向继续，`MoveDirection` 统一给动画和移动用。
4. **冲刺需要能在无键盘的环境下验证**：旧版 Input 无法在 batch mode 里伪造按键 —— `Player` 增加 `SetInputAxis(float?)` 和公开的 `StartDash()`，游戏本身的行为不变（不设置时仍然读键盘）。
5. **课程练习整个缺失**：新增 `Assets/Scripts/Exercises/` 下 8 个文件（7 个 C# 基础练习 + 1 个冷热游戏），每个都有「任务 / 解法 / 知识点」的头注释。
6. **练习没有配套文档**：新增 `docs/specs/raining-apocalypse-exercises.md`，逐个列出练习、代码与知识点。

## 核对过的既有接线（确认成立，未改动）

- `GameManager.PlayGame()` 原本就调用 `SceneManager.LoadScene("Game")`；主菜单 `PlayButton` 与失败画面的 Restart 按钮都通过持久调用指向它。
- `Enemy` 原本就用 `Random.Range(minSpeed, maxSpeed)` 取速度、`transform.Translate(Vector2.down * speed * Time.deltaTime)` 向下移动，`OnTriggerEnter2D` 也已经分开判断 `Player` 与 `Ground`。
- `Spawner` 原本就有 `Transform[] spawnPoints`、`GameObject[] hazards`，间隔按 `decrease` 递减到 `minTimeBetweenSpawns`，玩家对象消失（Unity 的假 null）后停止生成。
- `MusicManager` 原本就是 `DontDestroyOnLoad` 单例，主菜单的 `Music` 物体带 `Music.mp3`。
- `Ground` 标签在 `ProjectSettings/TagManager.asset` 里存在，`Game.unity` 里的 `Ground` 物体确实带这个标签，`CompareTag`/`tag` 比较不会在运行时抛异常。
- `Explosion.prefab` 已经有 `RandomSound` 组件，数组里是 `Explosion01`~`Explosion04` 四段音效；粒子系统的 `startLifetime`、`startSize`、`startColor`、`startSpeed` 都是随机值。
- 环境物体齐全：作者原版场景里云是 8 个（`Clouds_0` 与 `Clouds_0 (1)`~`(7)`），比清单上的 7 个多一个，保留原版场景不做删减。

## 验证

| 层 | 工具 | 结果 |
| --- | --- | --- |
| 缝一 静态接线 | `tools/check-wiring.ps1 -ProjectRoot tasks/05-raining-apocalypse` | 41 项全部 PASS |
| 缝二 Play 探针 | `tasks/05-raining-apocalypse/Assets/Editor/RainingApocalypseProbe.cs` | 报告里没有 FAIL |
| 缝三 构建冒烟 | 不在本票范围（打包票负责） | — |

探针断言的是外部可观察的行为：主菜单点 Play 进入 `Game` 场景、玩家左右移动并翻转、
冲刺把速度提到 `speed + extraSpeed` 且 `startDashTime` 后恢复、障碍物在生成点出现并下落、
两次生成之间的间隔越来越短、撞到地面的障碍物会炸掉但不掉血、撞到玩家掉 1 点血并更新血量文字、
血量归零后失败面板出现且 Restart 能开新一局、玩家消失后生成器停止生成、音乐管理器跨场景存活。

## 验收

达成条件全部满足后，导出 Windows 64 位玩家构建（itch.io 的 WebGL 发布为课程演示，本阶段以 Windows 构建为交付）。
