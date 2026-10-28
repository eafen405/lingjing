# Unity 工作流（实现者必读）

本仓库的每个交付物都是一个**独立的 Unity 工程**，位于 `tasks/0N-<slug>/`。
本文件记录所有实现者都必须遵守的调用规范与已踩过的坑。

## 编辑器

| 项 | 值 |
| --- | --- |
| 版本 | Unity **2022.3.62f2**（中国版 LTS） |
| 可执行文件 | `C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe` |
| 许可 | Unity Personal，已在 Unity Hub 中激活 |
| 后端 | Mono（**不要**装 IL2CPP） |
| 输入 | 旧版 Input Manager（`activeInputHandler: 0`） |

## 三条硬规矩

1. **必须带 `-projectPath`。** 不带的话 Unity 会把**当前工作目录**当成工程，
   在仓库根目录生成 `Assets/`、`Packages/`、`ProjectSettings/`。本仓库已经踩过一次。
2. **Play 模式探针不能加 `-quit`。** 加了 Unity 会在 `-executeMethod` 返回后立刻
   退出，Play 模式回调根本没机会跑。探针自己在写完报告后调用
   `EditorApplication.Exit(...)`。
3. **第一次运行会花 1~3 分钟导入工程**（生成 `Library/`）。之后才是增量编译。
   不要因为慢就以为卡死了。

## 写 `tools/*.ps1` 的两条硬规矩

1. **只写 ASCII。** Windows PowerShell 5.1 读取没有 BOM 的 `.ps1` 时按 ANSI（GBK）
   解码，文件里的中文字面量会被解码成乱码，**引号数量一变就变成未闭合的字符串**，
   整个脚本直接解析失败。注释里也不要写中文。
2. **仓库根目录用 `$PSScriptRoot` 推导**，不要写死绝对路径。

```powershell
$repoRoot = Split-Path -Parent $PSScriptRoot
```

## 跑 Unity 的唯一正确姿势

**永远不要**用 `& $ue ...` 直接启动：`Unity.exe` 是 Windows GUI 程序，PowerShell
**不会等待它**，命令会立刻返回，你会读到上一轮的旧日志。

用仓库自带的封装：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\user\Desktop\lingjing\tools\run-unity.ps1' `
    -ProjectPath 'tasks/01-challenge-2-play-fetch' `
    -Method 'Challenge2Probe.Run' `
    -MethodArgs "Assets/Challenge 2/Challenge 2.unity|$env:TEMP\probe.txt" `
    -LogPath "$env:TEMP\probe.log"
```

- `-MethodArgs` 是**管道符分隔**的字符串（`powershell -File` 无法传数组）。
- `-ProjectPath` 相对仓库根目录或绝对路径都行。
- 脚本会 `Start-Process -Wait`，返回真实退出码，并打印编译错误。

## 场景 / 预制体解析

不开编辑器就能看懂场景接线：

```powershell
powershell -NoProfile -File '<repo>\tools\dump-scene.ps1' `
    -ProjectRoot '<工程目录>' `
    -ScenePath '<场景 .unity 的绝对路径>'
```

输出：对象名、位置、旋转、标签、挂了哪些脚本、脚本的序列化字段。

Unity 的 YAML 是纯文本，也可以直接 `Select-String`。常用技巧：

- 找场景里某个脚本的实例：
  `m_Script: {fileID: 11500000, guid: <脚本.meta 里的 guid>`
- 脚本 GUID：`Get-Content 'Assets\...\Xxx.cs.meta' | Select-String 'guid: (\w+)'`
- 预制体的**根** GameObject：Transform 块里 `m_Father: {fileID: 0}` 的那个。
  引用预制体时必须指向**根**，指到子节点会得到一个残缺的实例。
- 场景里存的是 `{fileID: <该预制体文件内的对象 id>, guid: <预制体 guid>}`，
  **fileID 和 guid 都要对**，只改 guid 会指到预制体里的另一个对象。

## Play 模式探针（缝二）

模板：`tasks/01-challenge-2-play-fetch/Assets/Editor/Challenge2Probe.cs`

结构：

1. `[InitializeOnLoad]` 静态类订阅 `EditorApplication.playModeStateChanged`。
2. `-executeMethod` 入口 `Run()`：解析 `--` 之后的参数 → `SessionState` 存场景路径和
   报告路径 → `EditorSceneManager.OpenScene` → `EditorApplication.EnterPlaymode()`。
3. 进 Play 模式后挂一个 `DontDestroyOnLoad` 的宿主 MonoBehaviour 跑场景。
4. 跑完把 `PASS/FAIL` 清单写到报告文件，`EditorApplication.Exit(failed == 0 ? 0 : 1)`。

**坑：**

- `SessionState` 跨域重载存活，`static` 字段不会。参数必须走 `SessionState`。
- `EditorApplication.update` 事件在域重载后会被清空，所以看门狗要在
  `[InitializeOnLoad]` 的静态构造函数里重新订阅。
- 不要用 `GameObject.FindGameObjectsOfType<T>()`（2022.3 没有这个 API，
  `UnityEngine.Object.FindGameObjectsOfType<T>()` 也已移除）。改用
  `UnityEngine.Object.FindObjectsOfType<T>()`，或者更稳的：遍历
  `SceneManager.GetActiveScene().GetRootGameObjects()` 再
  `GetComponentsInChildren<T>()`。后者还能保证只看当前场景。
- `UnityEngine.Object` 和 `System.Object` 同名。文件里有 `using System;` 时，
  `Object.DestroyImmediate(...)` 会报 CS0104，写全 `UnityEngine.Object.DestroyImmediate(...)`。
- 旧版 `Input` 在无界面 batch mode 里**无法伪造按键**。探针要驱动游戏，
  就把"空格触发"的那段逻辑抽成一个 public 方法（例如
  `PlayerControllerX.SpawnDog()`），探针直接调它；空格绑定本身用
  `tools/check-wiring.ps1` 静态检查，再用**构建产物冒烟**（真实窗口 + 真实按键）
  补验。
- `Application.logMessageReceived` 在 batch mode 里能收到 `Debug.Log`，
  可以用来断言"球落地时输出 Game Over"。

## Unity 坐标与 `Transform.Translate`

- `transform.Translate(v)` 的 `space` 参数**默认是 `Space.Self`**，也就是沿着
  **自身**的局部轴移动，不是世界轴。
- 所以一个根节点绕 Y 转了 -90°（`m_LocalEulerAnglesHint: {x: 0, y: -90, z: 0}`）
  的预制体，脚本里的 `Vector3.forward` 实际是世界 **-X**。
  这就是挑战二里狗会向左跑的原因，**不要**把它改成 `transform.forward`。
- 相机在 `Main Camera` 上，rotation 为零就是看向世界 **+Z**；此时屏幕水平方向是
  世界 X 轴，屏幕竖直方向是世界 Y 轴。

## 碰撞体要按实测尺寸设置

不要凭观感调碰撞体，用 `Assets/Editor/MeasureTools.cs` 实测：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\user\Desktop\lingjing\tools\run-unity.ps1' `
    -ProjectPath 'tasks/01-challenge-2-play-fetch' -Method 'MeasureTools.Measure' `
    -MethodArgs 'Assets/Challenge 2/Prefabs/Dog.prefab' -LogPath "$env:TEMP\measure.log"
Select-String -Path "$env:TEMP\measure.log" -Pattern 'MEASURE '
```

输出里的 `LOCAL model size` / `center` 是**预制体根节点局部空间**下的模型包围盒，
`BoxCollider` 挂在根节点上时就用这组数。

注意：根节点带旋转时，世界空间 AABB 直接换算到局部空间是**错的**（负的 size）。
要逐个把 renderer bounds 的 8 个角点变换到局部空间再求 min/max。

## Git 工作流

见 `CONTRIBUTING.md`。要点：

- 分支：`feature/<issue 号>-<slug>`，从 `integration/phase-1` 切出。
- 提交信息用 Conventional Commits，小步提交。
- **不要重写已推送的历史。**
- 工作树（worktree）目录 `.worktrees/` 已在 `.gitignore` 里。

## 三层验证缝

| 层 | 工具 | 抓什么问题 |
| --- | --- | --- |
| 缝一 静态接线 | `tools/check-wiring.ps1` | 标签/构建场景/输入方式/预制体引用接错 |
| 缝二 Play 探针 | `Assets/Editor/xxxProbe.cs` | 行为不成立（狗不跑、球不生成、分数不更新） |
| 缝三 构建冒烟 | `tools/build-project.ps1` | 编辑器里好好的、导出来就坏 |

**顺序不能乱**：缝一 → 缝二 → 缝三。任何一层失败就停下来修。

## 只断言外部可观察行为

断言"玩家看得见的结果"：球在接触狗的瞬间消失、球落地时控制台出现 Game Over、
分数显示成 `Score: 12`。**不要**断言"某个私有变量被赋成了什么"。
