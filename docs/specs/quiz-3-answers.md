# Quiz 3 答案（Unit 3 - Sound and Effects）

来源：Unity Learn "Create with Code" / Unit 3 - Sound and Effects / Quiz 3
（<https://learn.unity.com/course/create-with-code/unit/unit-3-sound-and-effects/quiz/quiz-3-tt>，
10 题 / 15 分钟 / 及格线 1 题）

**答案来源说明**：该测验页面的 Markdown 版本（`quiz-3-tt.md`）**只给题目、不给答案**。
下表的答案取自同一页面 HTML 里内嵌的官方答案数据（`__NEXT_DATA__` 中每个选项的 `isCorrect`
标记与 `quizFeedback` 解析），并逐题与页面上的配图核对过（图 1–10 已下载后逐张确认），
不是自行推导的。下面的"理由"一栏是本次整理时补写的解释。

---

## 第 1 题 · `isAlive` 报错的原因

> 你想"玩家死亡后停止生成敌人"，写了 `EnemySpawner` 和 `PlayerController` 两个脚本，
> 但 `EnemySpawner` 里带下划线的 `isAlive` 报错了。是什么原因？

- 选项 A：`playerController.isAlive` 里的 `p` 应该大写
- 选项 B：`PlayerController` 类里的 `bool` 需要加 `public` 访问修饰符
- 选项 C：if 语句不能写在 `Update` 方法里
- 选项 D：`isAlive` 必须以大写 `I` 开头（`IsAlive`）

**答案：B** —— `PlayerController` 里的 `bool isAlive;` 没有写访问修饰符，C# 默认是 `private`，
所以 `EnemySpawner` 访问 `playerController.isAlive` 时报"不可访问"；改成 `public bool isAlive;` 即可。

---

## 第 2 题 · 动画方法与参数的配对

> 把下面的动画方法与它的参数集配对：
> 1. `anim.SetBool(__)` 2. `anim.SetTrigger(__)` 3. `anim.SetInt(__)`
> A. "Celebrate"  B. "Alive", true  C. "ThrowType", 2

**答案：1B, 2A, 3C** —— `SetBool` 需要"名字 + 布尔值"（"Alive", true），
`SetTrigger` 只需要一个名字（"Celebrate"），`SetInt` 需要"名字 + 整数"（"ThrowType", 2）。

---

## 第 3 题 · 从 `Idle` 过渡到 `Walk`

> 给定下面的动画状态机，哪一行代码能让角色从 `Idle` 状态过渡到 `Walk` 状态？
> （`Idle → Walk` 的过渡条件是 `Speed_f` Greater `0.25`）

- `setFloat("Speed_f", 0.3f);`
- `setInt("Speed_f", 1);`
- `setTrigger("Speed_f");`
- `setFloat("Speed_f", 0.1f);`

**答案：`setFloat("Speed_f", 0.3f);`** —— 过渡条件是浮点参数 `Speed_f > 0.25`，
只有把 `Speed_f` 设成 0.3 才满足；`setInt`/`setTrigger` 用错了参数类型，0.1 又不够大。

---

## 第 4 题 · 获取 `AudioSource` 引用

> 下面哪一行是正确的、在 GameObject 上取得 `AudioSource` 组件引用的写法？

- Line A：`audio = GetComponent<AudioSource>();`
- Line B：`audio = GetComponent(AudioSource)();`
- Line C：`audio = AudioSource.GetComponent<>();`
- Line D：`audio = GetComponent.Audio<Source>();`

**答案：Line A** —— `GetComponent<T>()` 是泛型方法，类型参数要写在尖括号里；
B 把尖括号写成了圆括号，C/D 的调用对象和语法都不对。

---

## 第 5 题 · `NullReferenceException` 的成因

> 运行下面的代码报 `NullReferenceException: Object reference not set to an instance of an object.`，最可能的原因是什么？
> （`Enemy.Start()` 里 `player = GameObject.Find("Player");`）

- Player 对象没有 Collider
- Enemy 对象没有 Rigidbody 组件
- `Start` 方法应该写成 `Update`
- 场景里没有名为 "Player" 的对象

**答案：场景里没有名为 "Player" 的对象** —— `GameObject.Find` 找不到对象时返回 `null`，
下一行访问 `player.transform` 就会抛空引用异常；这跟 Collider / Rigidbody 无关。

---

## 第 6 题 · "游戏未结束 **且** 玩家在地面"

> 下面哪个条件正确地表达了"游戏还没有结束，**并且**玩家在地面上"？

- Line A：`if (gameOver == false AND isOnGround)`
- Line B：`if (gameOver && isOnGround == true)`
- Line C：`if (gameOver != true && isOnGround)`
- Line D：`if (gameOver != false && isOnGround == true)`

**答案：Line C** —— `gameOver != true` 就是"游戏未结束"，`&&` 才是"并且"；
A 里的 `AND` 不是 C# 运算符（写出来直接编译不过），B 的逻辑正好相反（要求游戏已结束），
D 表示"游戏已结束"。

---

## 第 7 题 · 动画控制器的默认状态

> 这个 Animation Controller 默认第一个会播放哪个状态？

- "Any State"
- "NotCrouched"
- "Death"
- "Crouch_Up"

**答案："NotCrouched"** —— Entry（入口）的箭头指向 `NotCrouched`，它是默认状态；
`Any State` 是特殊节点，不算状态机里的第一个状态。

---

## 第 8 题 · 符合 Unity 命名规范的变量声明

> 下面哪个变量声明符合 Unity 的标准命名规范（尤其是大小写）？

- 选项组合：2 和 4 / 3 和 6 / 4 和 5 / 1 和 5
- 1. `private Animator anim;` 2. `private player Player;` 3. `Float JumpForce = 10.0f;`
  4. `bool gameOver = True;` 5. `private Vector3 startPos;` 6. `Public gameObject ObstaclePrefab;`

**答案：1 和 5** —— 私有成员变量用小驼峰（`anim`、`startPos`），类型名用大驼峰；
2 把类型名 `player` 写成小写，3 把类型名 `Float` 写成大写，4 的布尔字面量必须是小写 `true`，
6 的访问修饰符必须是小写 `public`。

---

## 第 9 题 · `Run` 与 `Walk` 之间的过渡条件

> 下图所示的 `Run`/`Walk` 过渡，最可能的条件是什么？

- Jump_b is true
- Speed_f is Less than 0.5
- Speed_f is Greater than 0.5
- Animation_int is Less than 10

**答案：Speed_f is Less than 0.5** —— 从 Running 变回 Walking 说明速度降下来了，
所以条件应该是浮点参数 `Speed_f < 0.5`（官方解析也是这么写的）。

---

## 第 10 题 · 简单的移动状态机

> 下面哪个动画状态机对"简单的移动状态机"来说最合理？（Image A / Image B / Image C）

**答案：Image A** —— 官方解析：应该以 `Idle` 作为默认状态（Entry 指向 `Idle`），
然后三个状态（Idle / Walk / Run）之间都能互相过渡，`Walk` 与 `Run` 之间也一定要有过渡。
Image A 正是这样；Image B 的 Entry 指向 `Walk`，`Idle` 永远进不去；
Image C 缺少 `Walk` 与 `Run` 之间的过渡。
