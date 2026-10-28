# 灵境竞赛组 · AI+前端方向 第一阶段

Unity 入门课程考核产物：**《Create with Code》的四个挑战** + **一款完整的 2D 游戏
Raining Apocalypse**，每个都是一个可独立运行、可独立打包的 Unity 工程。

考核要求里写得很直接——"制作并打包"。这个仓库要能回答三个问题：

1. **做了什么**：每个交付物对应哪份官方题目，逐条对照清单见 `docs/specs/`。
2. **对不对**：每个交付物都有三层自动化验证，绿了才算完成。
3. **是不是我做的**：Git 提交历史保留真实开发过程，不是一口气倒上去的。

---

## 交付物一览

| # | 工程目录 | 交付物 | 官方题目 | 验收清单 |
| --- | --- | --- | --- | --- |
| 1 | `tasks/01-challenge-2-play-fetch/` | Challenge 2 · Play Fetch | Unity Learn "Create with Code" / Unit 2 | [清单](docs/specs/challenge-2-play-fetch.md) |
| 2 | `tasks/02-challenge-3-balloons-bombs-booleans/` | Challenge 3 · Balloons, Bombs & Booleans | 同上 / Unit 3 | [清单](docs/specs/challenge-3-balloons-bombs-booleans.md) |
| 3 | `tasks/03-challenge-4-soccer-scripting/` | Challenge 4 · Soccer Scripting | 同上 / Unit 4 | [清单](docs/specs/challenge-4-soccer-scripting.md) |
| 4 | `tasks/04-challenge-5-whack-a-food/` | Challenge 5 · Whack-a-Food | 同上 / Unit 5 | [清单](docs/specs/challenge-5-whack-a-food.md) |
| 5 | `tasks/05-raining-apocalypse/` | Raining Apocalypse（完整游戏） | Udemy "Make Your First 2D Game with Unity & C#" | [清单](docs/specs/raining-apocalypse.md) |

每个清单里都勾着三样东西：**官方达成条件**、**起始文件里的缺陷**、**加分项**，
以及每一条的修复说明。

## 怎么跑起来

**最省事**：交付包的 `02-players/` 里是五个独立的 Windows 64 位程序，双击即玩，不用装 Unity。
每个游戏的操作与玩法见 **[docs/delivery/how-to-play.md](docs/delivery/how-to-play.md)**。

想在 Unity 里改代码的话，需要 Unity **2022.3.62f2**（LTS）与 Unity Personal 许可：

```bash
# 用 Unity Hub 打开任意一个工程目录，例如：
#   tasks/01-challenge-2-play-fetch
# 然后按 Play 即可。
```

工程已经预先配好：自定义标签、构建场景列表、旧版 Input Manager、产品名，
所以打开就能直接运行，不需要先在编辑器里点一遍设置。

导出 Windows 64 位玩家构建：

```powershell
powershell -File tools/build-project.ps1 -ProjectPath 'tasks/01-challenge-2-play-fetch'
```

## 怎么验证（三层缝）

| 层 | 工具 | 抓什么问题 | 怎么跑 |
| --- | --- | --- | --- |
| 缝一 静态接线 | `tools/check-wiring.ps1` | 标签缺失、构建场景指错、输入方式被改、预制体引用接错 | `powershell -File tools/check-wiring.ps1 -ProjectRoot 'tasks/01-challenge-2-play-fetch'` |
| 缝二 Play 模式探针 | 各工程 `Assets/Editor/*Probe.cs` | 行为不成立：球不生成、狗不跑、分数不更新 | `powershell -File tools/run-unity.ps1 -ProjectPath 'tasks/01-...' -Method 'Challenge2Probe.Run' -MethodArgs "Assets/Challenge 2/Challenge 2.unity|$env:TEMP\probe.txt"` |
| 缝三 构建冒烟 | `tools/build-project.ps1` + `tools/smoke-test.ps1` | 编辑器里好好的、导出来就坏 | 见 `docs/agents/unity-workflow.md` |

**顺序不能乱**：缝一 → 缝二 → 缝三。任何一层失败就停下来修，不往下堆。

探针断言的都是**外部可观察行为**——玩家看得见、听得见、玩得到的结果。
断言"这个变量被赋成了什么"没有意义，断言"球在接触狗的瞬间消失"才有意义。

每个工程的具体验证结果写在各自的验收清单里。

## 仓库结构

```
tasks/01..05/        五个独立的 Unity 工程（各含 Assets / Packages / ProjectSettings）
tools/               工程初始化、场景解析、构建、冒烟、静态检查脚本
  wiring/            每个工程一份静态接线检查的预期值（JSON）
docs/specs/          规格与逐条验收清单
docs/adr/            不可轻易回退的决策记录（编辑器版本、工程拆分、技术原貌）
docs/agents/         给协作者看的 Unity 调用规范与坑位清单
GLOSSARY.md          领域词汇表（考核任务 / 挑战 / 达成条件 / 加分项 / 交付包 …）
```

## 为什么是这五个独立的工程

四个挑战虽然出自同一门课，但素材、脚本、场景互不通用，而且考核要求**分别打包**。
合成一个大工程会让每个交付物的构建互相干扰，也会让"哪个成果对应哪道题"变得不清楚。

详见 [ADR 0002](docs/adr/0002-five-unity-projects.md)。

## 为什么不用 Unity 6、不用新 Input System

- Unity 6 全系安装包在中国区 CDN 全部 404，直链与 Unity Hub 两条路都取不到。
- 2021.3 版的起始文件与课程同代，本来就是旧版输入。
- 保持教程的技术原貌，是为了让评审对照官方录像时看到的是同一套东西。

详见 [ADR 0001](docs/adr/0001-unity-2022-3-lts.md) 与
[ADR 0003](docs/adr/0003-fidelity-over-modernization.md)。

## 提交规范

见 [CONTRIBUTING.md](CONTRIBUTING.md)：issue 先行 → `feature/<issue 号>-<slug>` 分支 →
小步 Conventional Commits → PR 合并。**不重写已推送的历史**——评分要求保留真实开发过程，
改写历史反而像事后编造。
