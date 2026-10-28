# Challenge 3 · Balloons, Bombs, & Booleans

来源：Unity Learn "Create with Code" / Unit 3 - Sound and Effects
对应视频：[Create With Code 录播合集](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 6P「5月28日 Challenge 3 & Quiz 3」
工程目录：`tasks/02-challenge-3-balloons-bombs-booleans/`

## 目标

气球在滚动的小镇里向上飘，吃金币、躲炸弹。

## 达成条件

- [x] 按住空格键气球持续上升
- [x] 背景无缝重复滚动，制造前进/上升的错觉
- [x] 炸弹与金币按随机计时生成
- [x] 吃到金币：播放粒子效果与音效
- [x] 撞到炸弹：播放爆炸效果，背景停止滚动

## 起始文件中的缺陷（逐条修复）

- [x] 玩家无法操控气球（`rigidBody` 空引用，`Start` 里没有用 `GetComponent` 赋值）
  → `PlayerControllerX.Start()` 里补上 `playerRb = GetComponent<Rigidbody>()`，控制气球不再抛空引用异常。
- [x] 背景只在游戏结束时才移动（`MoveLeftX.cs` 的开始/停止逻辑写反）
  → 条件由 `if (playerControllerScript.gameOver)` 改成 `if (!playerControllerScript.gameOver)`：游戏进行中滚动，结束后停止。
- [x] 什么都不生成（`SpawnManagerX` 里 `PrawnsObject` 拼写错误）
  → `InvokeRepeating("PrawnsObject", …)` 改成 `InvokeRepeating("SpawnObjects", …)`，炸弹与金币按 1.5 秒的定时器随机生成。
- [x] 烟花出现在气球旁边而不是气球上（粒子是玩家的子物体，位置需显式设为气球位置）
  → 吃到金币时先 `fireworksParticle.transform.position = transform.position;` 再 `Play()`。
- [x] 背景不无缝重复（`repeatWidth` 应取背景宽度的**一半**，不是高度）
  → `RepeatBackgroundX.Start()` 改用 `GetComponent<BoxCollider>().size.x / 2`（背景 BoxCollider 宽 112.8，重复点为 x = 45 - 56.4）。

## 加分项

- [x] 限制气球上升的最大高度（引入类似 `isLowEnough` 的布尔量）
  → 新增 `public bool isLowEnough` 与 `public float maxHeight = 14f`：`FixedUpdate` 里按高度刷新 `isLowEnough`，
    到达上限后不再加向上力、清零上升速度并把气球按在上限处（同时限制最大上升速度，避免一步冲出画面）。
- [x] 气球撞到地面时弹起，并播放音效
  → 场景里的 `Ground` 原本只有不可用的非凸 MeshCollider：已关掉它，并按地面尺寸补了一个
    BoxCollider（局部 10 × 0.2 × 10，缩放后 30 × 0.2 × 10，顶面正好在 y = 0），同时给 `Ground` 加了 `Ground` 标签；
    `PlayerControllerX.OnCollisionEnter` 里碰到 `Ground` 时先清掉向下的速度再 `AddForce(Vector3.up * bounceForce, ForceMode.Impulse)`，
    并播放 `bounceSound`（Boing.wav，场景里已挂好）。

## Quiz 3

课程在这一集附 Quiz 3（10 题 / 15 分钟）。考点覆盖：`isAlive` 的访问修饰符与大小写、动画参数（bool/float/int/trigger）、`Idle`→`Walk` 过渡代码、获取 `AudioSource` 引用、`NullReferenceException` 的成因、"游戏未结束 **且** 玩家在地面" 的条件写法、Animator 默认状态、Unity 命名规范、`Speed_f` 阈值过渡、移动状态机的合理性。

答案已整理到 [`quiz-3-answers.md`](quiz-3-answers.md)。

## 验证

- 缝一（静态接线）：`tools/check-wiring.ps1 -ProjectRoot tasks/02-challenge-3-balloons-bombs-booleans` → 23 项全 PASS，退出码 0。
- 缝二（Play 探针）：`tasks/02-challenge-3-balloons-bombs-booleans/Assets/Editor/Challenge3Probe.cs`
  → 进入 Play 模式后断言「气球能被操控且不越过上限、背景滚动/无缝重复/结束即停、定时生成炸弹与金币、
  烟花出现在气球位置、炸弹爆炸并结束回合、气球落地弹起并出声」，报告中无 FAIL。

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
