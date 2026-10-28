# Challenge 5 · Whack-a-Food

来源：Unity Learn "Create with Code" / Unit 5 - User Interface
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 10P「7月2日 Challenge 5」
工程目录：`tasks/04-challenge-5-whack-a-food/`

## 目标

打地鼠式的 UI 游戏：网格里不断弹出食物，点中得分，同时避开骷髅。

## 达成条件

- [ ] 按钮上的文字水平垂直都居中对齐
- [ ] 选择难度会改变食物的生成速率
- [ ] 点击食物时销毁它，并更新左上角的分数
- [ ] 游戏结束时出现 Restart 按钮

## 起始文件中的缺陷（逐条修复）

- [ ] 难度按钮排版很乱（需要把 `Text` 子物体水平垂直居中）
- [ ] 食物过早被销毁（应在**点击**时销毁，而不是鼠标**移入**时）
- [ ] 分数位置显示的是字面量 `score`，而不是 `Score: <数值>`（字符串拼接问题）
- [ ] 没办法重开（`GameOver()` 里需要重新激活 Restart 按钮）
- [ ] 难度按钮点了没反应（按钮脚本没有把参数传给 Game Manager；`spawnRate` 也过快）

## 加分项

- [ ] 从 60 秒开始的倒计时显示（`Time.deltaTime` 配合 `Mathf.Round` 取整秒），归零触发游戏结束

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
