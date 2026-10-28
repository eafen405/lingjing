# Challenge 2 · Play Fetch

来源：Unity Learn "Create with Code" / Unit 2 - Basic Gameplay
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 4P「5月14日 Challenge2」
工程目录：`tasks/01-challenge-2-play-fetch/`

## 目标

狗接住从天上掉落的球。

## 达成条件

- [ ] 屏幕上随机生成三种子球之一，位置在屏幕上方，水平坐标随机
- [ ] 按下空格键生成一只狗，狗朝球跑去
- [ ] 狗与球碰撞时销毁球
- [ ] 球落到地面时在控制台输出 `Game Over`
- [ ] 狗和球离开屏幕边界时被销毁

## 起始文件中的缺陷（逐条修复）

- [ ] 球生成的高度不对
- [ ] 生成出来的是绿色球，不是狗
- [ ] 球只是靠近狗就被销毁（碰撞体与触发器设置错误）
- [ ] 离开屏幕不会被销毁（`DestroyOutOfBounds` 的边界变量与比较符写错）
- [ ] 永远只生成第 1 种球（需用随机整数索引去取 `Instantiate` 的对象）

## 加分项

- [ ] 生成间隔在 3~5 秒之间随机（`InvokeRepeating` 做不到，需要 `Invoke` 加自调度）
- [ ] 加冷却，防止连按空格无限刷狗

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
