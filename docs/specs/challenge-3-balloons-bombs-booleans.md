# Challenge 3 · Balloons, Bombs, & Booleans

来源：Unity Learn "Create with Code" / Unit 3 - Sound and Effects
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 6P「5月28日 Challenge 3 & Quiz 3」
工程目录：`tasks/02-challenge-3-balloons-bombs-booleans/`

## 目标

气球在滚动的小镇里向上飘，吃金币、躲炸弹。

## 达成条件

- [ ] 按住空格键气球持续上升
- [ ] 背景无缝重复滚动，制造前进/上升的错觉
- [ ] 炸弹与金币按随机计时生成
- [ ] 吃到金币：播放粒子效果与音效
- [ ] 撞到炸弹：播放爆炸效果，背景停止滚动

## 起始文件中的缺陷（逐条修复）

- [ ] 玩家无法操控气球（`rigidBody` 空引用，`Start` 里没有用 `GetComponent` 赋值）
- [ ] 背景只在游戏结束时才移动（`MoveLeftX.cs` 的开始/停止逻辑写反）
- [ ] 什么都不生成（`SpawnManagerX` 里 `PrawnsObject` 拼写错误）
- [ ] 烟花出现在气球旁边而不是气球上（粒子是玩家的子物体，位置需显式设为气球位置）
- [ ] 背景不无缝重复（`repeatWidth` 应取背景宽度的**一半**，不是高度）

## 加分项

- [ ] 限制气球上升的最大高度（引入类似 `isLowEnough` 的布尔量）
- [ ] 气球撞到地面时弹起，并播放音效

## Quiz 3

课程在这一集附 Quiz 3（10 题 / 15 分钟）。考点覆盖：`isAlive` 的访问修饰符与大小写、动画参数（bool/float/int/trigger）、`Idle`→`Walk` 过渡代码、获取 `AudioSource` 引用、`NullReferenceException` 的成因、"游戏未结束 **且** 玩家在地面" 的条件写法、Animator 默认状态、Unity 命名规范、`Speed_f` 阈值过渡、移动状态机的合理性。

需要把答案写进交付文档。

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
