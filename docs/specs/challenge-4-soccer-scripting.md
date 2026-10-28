# Challenge 4 · Soccer Scripting

来源：Unity Learn "Create with Code" / Unit 4 - Gameplay Mechanics
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 8P「6月11日 Challenge 4」
工程目录：`tasks/03-challenge-4-soccer-scripting/`

## 目标

复用 Unit 4「Sumo Battle」的操作技巧，用足球对抗：绕着摄像机操控球，把敌方球撞进**对方**的球门，同时防住它们撞进**你的**球门。

## 达成条件

- [ ] 敌方球朝我的球门移动，但能被撞开
- [ ] 道具提供临时的力量加成，5 秒后消失
- [ ] 敌人全部被清空后生成新一波，且比上一波多一个敌人

## 起始文件中的缺陷（逐条修复）

- [ ] 撞到敌人反而把它送回自己这边（`PlayerControllerX.cs` 里向量相减的方向写反）
- [ ] 玩家一吃到道具就刷怪（应在敌人被清空时刷，用 `enemyCount` 判断）
- [ ] 道具永远不消失（`PowerupCoolDown` 协程没有用 `StartCoroutine` 调用）
- [ ] 每波固定刷 2 个敌人（for 循环应使用 `enemiesToSpawn`：第 1 波 1 个，第 2 波 2 个，依此类推）
- [ ] 敌方球完全不动（`EnemyX.cs` 空引用，`playerGoal` 从未赋值）

## 加分项

- [ ] 空格键加速冲刺，Focal Point 下方挂粒子效果
- [ ] 敌人速度每波略微提升

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
