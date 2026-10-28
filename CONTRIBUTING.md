# Contributing

灵境竞赛组 AI+前端方向第一阶段。本仓库同时存放交付源码与过程文档，所以提交历史本身就是考核材料的一部分。

## 工作流

1. **先开 issue**。每个可交付的工作项在 GitHub Issues 里跟踪（`docs/agents/issue-tracker.md`），带上 `ready-for-agent` 标签。
2. **再开分支**。命名 `feature/<issue-number>-<slug>`，例如 `feature/1-challenge-2-play-fetch`。
3. **小步提交**。一次提交只做一件事，message 用 Conventional Commits：

   ```
   feat(challenge-3): add height limit for the balloon
   fix(challenge-4): correct the away-from-player vector
   chore(tools): add scene dump helper
   ```

4. **提 PR 合并到 main**，PR 里勾选验收项，并引用对应 issue（`Closes #3`）。
5. **绝不重写已推送的历史**。评分要求是"保留真实开发历史"，force-push 会让它看起来像事后编造。

多个交付物并行开发时，ticket 分支先合进集成分支 `integration/phase-1`，集成分支再整体合进 `main`。
合并提交用 `merge(<范围>): <说明>` 的写法，例如 `merge(challenge-3): integrate the Balloons ticket`——
它不是 Conventional Commits 的标准 type，但合并提交本来就不是某一个作者写的，
统一成一种写法便于阅读。

## 提交纪律

- 一次提交只包含一个逻辑变更；不要把"下载素材 + 改脚本 + 加文档"混在一起。
- 不要提交 Unity 生成物：`Library/`、`Temp/`、`obj/`、`Build/`、`Logs/`、`UserSettings/`、`*.csproj`、`*.sln` 都已在 `.gitignore` 里。
- Unity 的 `.meta` 文件**必须**提交——它们保存资源 GUID，丢了会导致场景引用断裂。
- 提交前先跑一次构建验证，确认没有编译错误。

## 构建验证

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
$unity -batchmode -quit -nographics -projectPath tasks/01-challenge-2-play-fetch -logFile -
```

> 务必带 `-projectPath`。不带的话 Unity 会把**当前工作目录**当成工程，在仓库根目录生成 `Assets/`、`Packages/`、`ProjectSettings/`——这是本仓库踩过的坑。

## 目录约定

| 路径 | 内容 |
| --- | --- |
| `tasks/01`~`tasks/04` | Create with Code 课程的四个挑战，各自独立成 Unity 工程 |
| `tasks/05` | 完整游戏 Raining Apocalypse |
| `docs/specs/` | 每个交付物的验收清单（达成条件 / 缺陷 / 加分项） |
| `docs/adr/` | 不可轻易回退的决策记录 |
| `tools/` | 工程辅助脚本（场景解析、工程初始化、录像抽帧） |
