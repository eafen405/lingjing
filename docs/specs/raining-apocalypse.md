# Raining Apocalypse（完整游戏）

来源：Udemy "Make Your First 2D Game with Unity & C# – Beginner Course"（Noa Calice / Blackthornprod）
对应视频：[Unity 2D 游戏开发入门：C# 零基础完整实战](https://www.bilibili.com/video/BV1xegN6DEv9) 第 27P 起「04-制作游戏」
工程目录：`tasks/05-raining-apocalypse/`

作者公开的工程：`https://github.com/Liam-Calice/MakeYourFirstGame`（原始工程版本为 Unity 2018.3.0f2）

## 目标

一款 2D 躲避游戏：玩家左右移动，天上不断落下障碍物，坚持存活。课程最终把它发布成 WebGL 版本到 itch.io。

## 达成条件

### 场景与流程

- [ ] 两个场景：`MainMenu.unity` 与 `Game.unity`，都在 Build Settings 里
- [ ] `GameManager.PlayGame()` 调用 `SceneManager.LoadScene("Game")` 进入游戏
- [ ] 游戏结束显示 LosePanel，可 Restart

### 玩家

- [ ] Rigidbody2D，方向键左右移动
- [ ] `isRunning` 动画 bool 参数驱动 idle/run 过渡
- [ ] 移动时精灵按方向翻转
- [ ] `health` 数值与 `TakeDamage(int)` 方法
- [ ] 血量在 UI 上显示，归零进入失败画面

### 障碍物

- [ ] 三个障碍物预制体：`Hazard.prefab`、`Hazard 2.prefab`、`Hazard 3.prefab`
- [ ] 速度在 `[min, max]` 区间随机，向下移动
- [ ] 用 `OnTriggerEnter2D` 区分撞到 `Player` 与撞到 `Ground`

### 生成器

- [ ] `Spawner.cs`：`Transform[] spawnPoints`、`GameObject[] hazards`
- [ ] 生成间隔随时间递减（`startTimeBtwSpawns` → `minTimeBetweenSpawns`，每步 `decrease`）
- [ ] 玩家对象消失时停止生成

### 美术与动画

- [ ] 自己制作的 2D 美术，玩家切成 Head / Body / Arm Left / Arm Right / Leg Left / Leg Right
- [ ] `Player.controller` 动画控制器，含 `idle.anim` 与 `run.anim`，靠 `isRunning` 过渡
- [ ] 环境：`Ground`、`WallLeft`、`WallRight`、`Clouds` ×7、`Mountains` ×3

### 粒子

- [ ] `Explosion.prefab` 粒子系统，颜色/大小/生命周期随机，碰撞时触发

### UI（UGUI）

- [ ] `Canvas` + `EventSystem`
- [ ] 血量 `Text`、`Image`、`Button`（`PlayButton`）
- [ ] 主菜单含 Logo 与 Play 按钮
- [ ] 失败画面含 Restart 按钮

### 音频

- [ ] `Explosion01`~`Explosion04.wav`、`Game Over.wav`、`Hurt.mp3`、`Music.mp3`
- [ ] `RandomSound.cs`：从数组中随机取一段播放
- [ ] `MusicManager.cs`：`DontDestroyOnLoad` 单例

### 冲刺移动

- [ ] `startDashTime`、`dashTime`、`extraSpeed`、`dashing` 标志位，配合 `Time.deltaTime`
- [ ] 注意：作者公开的工程里**没有**这一项，需要自己实现

### 发布

- [ ] 导出 WebGL 构建并发布到 itch.io（课程第 37 节）

## 课程额外内容

- [ ] 「冷热游戏」控制台练习（随机 0~100，给出冷热反馈）
- [ ] C# 基础部分的约 7 个编程练习

## 验收

达成条件全部满足后，导出 Windows 64 位玩家构建（itch.io 的 WebGL 发布为课程演示，本阶段以 Windows 构建为交付）。
