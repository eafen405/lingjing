# Challenge 5 · Whack-a-Food

来源：Unity Learn "Create with Code" / Unit 5 - User Interface
对应视频：[Create With Code 录播合辑](https://www.bilibili.com/video/BV1YD4y1S7ng) 第 10P「7月2日 Challenge 5」
工程目录：`tasks/04-challenge-5-whack-a-food/`
官方题目：[Challenge 5 - Whack-a-Food](https://learn.unity.com/course/create-with-code/unit/unit-5-user-interface/tutorial/challenge-5-whack-a-food?version=2021.3)

## 目标

打地鼠式的 UI 游戏：网格里不断弹出食物，点中得分，同时避开骷髅。

## 达成条件

- [x] 按钮上的文字水平垂直都居中对齐
- [x] 选择难度会改变食物的生成速率
- [x] 点击食物时销毁它，并更新左上角的分数
- [x] 游戏结束时出现 Restart 按钮

## 起始文件中的缺陷（逐条修复）

- [x] 难度按钮排版很乱（需要把 `Text` 子物体水平垂直居中）
- [x] 食物过早被销毁（应在**点击**时销毁，而不是鼠标**移入**时）
- [x] 分数位置显示的是字面量 `score`，而不是 `Score: <数值>`（字符串拼接问题）
- [x] 没办法重开（`GameOver()` 里需要重新激活 Restart 按钮）
- [x] 难度按钮点了没反应（按钮脚本没有把参数传给 Game Manager；`spawnRate` 也过快）

## 加分项

- [x] 从 60 秒开始的倒计时显示（`Time.deltaTime` 配合 `Mathf.Round` 取整秒），归零触发游戏结束

## 修复记录

- `GameManagerX.UpdateScore`：分数文本写死成字面量 `score` → 改成 `"Score: " + score`。
- `GameManagerX.GameOver`：`restartButton.gameObject.SetActive(false)` 把重开按钮藏了起来 → 改成 `SetActive(true)`。
- `GameManagerX.StartGame`：没有参数、直接 `spawnRate /= 5`（点得越多越快）→ 改成 `StartGame(int difficulty)`，按难度取生成间隔（Easy 2 秒 / Medium 1.5 秒 / Hard 0.75 秒）。
- `DifficultyButtonX.SetDifficulty`：点击时没有把难度传给 Game Manager → 改成 `gameManagerX.StartGame(difficulty)`。
- `TargetX`：食物在 `OnMouseEnter`（鼠标**移入**）时就被销毁 → 改成在 `OnMouseDown`（**点击**）时销毁、计分并播放爆炸。
- `Challenge 5.unity`：三个难度按钮的 `Text` 子物体对齐各不相同（`UpperCenter` / `LowerLeft` / `LowerRight`）→ 全部改成 `MiddleCenter`（`m_Alignment: 4`）。
- `Challenge 5.unity` + `GameManagerX`：新增 `Time Text`（TextMeshProUGUI）倒计时对象，由 Game Manager 每帧用 `Time.deltaTime` 递减、`Mathf.Round` 取整秒显示，归零时调用 `GameOver()` 结束这一局。
- `Assets/TextMesh Pro/`：工程原本没有导入 TextMeshPro Essential Resources，`TMP_Settings` 为空会让 UI 在运行时报警 → 从 `com.unity.textmeshpro` 包里的 `TMP Essential Resources.unitypackage` 导入（设置、默认字体、TMP 着色器）。
- `Packages/manifest.json`：仓库里的清单只列了 5 个包，Unity 打开工程后会写回完整的包列表（含 `com.unity.modules.physics` 等内置模块）→ 把编辑器解析出来的清单提交，避免脚本编译失败。

## 验证

三层缝，见 `docs/agents/unity-workflow.md`：

| 层 | 工具 | 结果 |
| --- | --- | --- |
| 缝一 静态接线 | `tools/check-wiring.ps1` | 17 项全通过 |
| 缝二 Play 探针 | `Assets/Editor/Challenge5Probe.cs` | 38 项全通过（`checks: 38  failed: 0`） |
| 缝三 构建冒烟 | `tools/build-project.ps1` | 由打包阶段统一执行 |

探针断言的是外部可观察行为：难度按钮标签的水平/垂直居中、Easy 与 Hard 的生成速度不同、点击食物后食物消失且分数变成 `Score: 5`、只有鼠标经过的食物仍在且不计分、倒计时从 `Time: 60` 递减到 `Time: 0`、归零后出现 Restart 按钮和 Game Over 文字。

探针通过真实的 `Button.onClick` 驱动难度按钮，通过真实的 `OnMouseDown` 回调模拟点击（无界面 batch mode 无法伪造鼠标输入）。

## 验收

达成条件全部满足，且加分项全部实现后，用 Unity 导出 Windows 64 位玩家构建。
