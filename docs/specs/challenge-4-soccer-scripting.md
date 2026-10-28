# Challenge 4 · Soccer Scripting

来源：Unity Learn "Create with Code" / Unit 4 - Gameplay Mechanics
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 8P「6月11日 Challenge 4」
工程目录：`tasks/03-challenge-4-soccer-scripting/`

## 目标

复用 Unit 4「Sumo Battle」的操作技巧，用足球对抗：绕着摄像机操控球，把敌方球撞进**对方**的球门，同时防住它们撞进**你的**球门。

## 达成条件

- [x] 敌方球朝我的球门移动，但能被撞开
- [x] 道具提供临时的力量加成，5 秒后消失
- [x] 敌人全部被清空后生成新一波，且比上一波多一个敌人

## 起始文件中的缺陷（逐条修复）

- [x] 撞到敌人反而把它送回自己这边（`PlayerControllerX.cs` 里向量相减的方向写反）
      → `awayFromPlayer` 改成 `other.gameObject.transform.position - transform.position`，敌人被推离玩家。
- [x] 玩家一吃到道具就刷怪（应在敌人被清空时刷，用 `enemyCount` 判断）
      → `enemyCount` 改为统计 `"Enemy"` 标签，新一波只在敌人被清空后生成。
- [x] 道具永远不消失（`PowerupCoolDown` 协程没有用 `StartCoroutine` 调用）
      → 吃到道具时调用 `StartCoroutine(PowerupCooldown())`，5 秒后力量加成与指示器一起关闭。
- [x] 每波固定刷 2 个敌人（for 循环应使用 `enemiesToSpawn`：第 1 波 1 个，第 2 波 2 个，依此类推）
      → for 循环改用 `enemiesToSpawn` 参数，第 1 波 1 个、第 2 波 2 个。
- [x] 敌方球完全不动（`EnemyX.cs` 空引用，`playerGoal` 从未赋值）
      → `Start()` 里 `playerGoal = GameObject.Find("Player Goal")`；`Enemy.prefab` 的 `speed` 由 0 改为 500，
        `Update` 改为 `FixedUpdate` + `Time.fixedDeltaTime`（推力必须大于地面 0.6 的静摩擦，球才会滚动），
        施力前先 `WakeUp()`（睡着的刚体会忽略推力）。

## 加分项

- [x] 空格键加速冲刺，Focal Point 下方挂粒子效果
      → `Input.GetKeyDown(KeyCode.Space)` 调用 `PlayerControllerX.Dash()`：沿 Focal Point 前向给玩家一次冲量，
        同时播放 `Smoke_Particle`；该预制体已挂到 `Focal Point` 之下（`Challenge4SceneSetup.Apply` 完成接线）。
        冲量前先 `WakeUp()`，否则站住不动的玩家会忽略冲刺。
- [x] 敌人速度每波略微提升
      → `SpawnManagerX.enemySpeed` 每波增加 10，生成敌人时写入 `EnemyX.speed`。

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。

验证方式（缝一 + 缝二）：

```powershell
powershell -File tools/check-wiring.ps1 -ProjectRoot 'tasks/03-challenge-4-soccer-scripting'
powershell -File tools/run-unity.ps1 -ProjectPath 'tasks/03-challenge-4-soccer-scripting' `
    -Method 'Challenge4Probe.Run' `
    -MethodArgs "Assets/Challenge 4/Challenge 4.unity|$env:TEMP\challenge4-probe.txt" `
    -LogPath "$env:TEMP\challenge4-probe.log"
```
