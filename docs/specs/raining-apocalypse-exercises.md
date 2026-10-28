# Raining Apocalypse 课程额外练习

来源：Udemy "Make Your First 2D Game with Unity & C# – Beginner Course"（Noa Calice / Blackthornprod）
对应视频：[Unity 2D 游戏开发入门：C# 零基础完整实战](https://www.bilibili.com/video/BV1xegN6DEv9)
代码目录：`tasks/05-raining-apocalypse/Assets/Scripts/Exercises/`

课程正文之外还有两类练习：**一个「冷热游戏」控制台练习**和**C# 基础部分的约 7 个编程练习**。
它们不在游戏流程里，而是教学用的独立小文件：每个文件只讲一个知识点，
文件头用注释写清楚「任务 / 解法 / 知识点」，可以直接在 Unity 控制台里跑 `Run()` 看结果。

## 怎么跑

在 Unity 编辑器里用下面的方式调用（也可以挂到一个临时物体上）：

```csharp
Exercise01Variables.Run();      // 打印变量与字符串
Exercise02Arithmetic.Run();     // 打印算术结果
Exercise03SwapVariables.Run();  // 打印交换后的两个数
Exercise04OddOrEven.Run();      // 打印奇偶判断
Exercise05LargestOfThree.Run(); // 打印三个数的最大值和最小值
Exercise06FizzBuzz.Run();       // 打印前 15 行 FizzBuzz
Exercise07ReverseString.Run();  // 打印反转后的字符串
HotAndColdGame.Run();           // 演示冷热反馈
```

`HotAndColdGame.PlayInConsole()` 是真正的「控制台游戏」：它会反复读入玩家输入，
直到猜中为止。只在编辑器里手动调用，不影响游戏本身。

## 练习清单

| # | 文件 | 练习 | 知识点 |
| --- | --- | --- | --- |
| 1 | `Exercise01Variables.cs` | 变量与字符串 | 变量声明、基本类型、字符串插值 |
| 2 | `Exercise02Arithmetic.cs` | 算术运算 | `+ - * / %`、整数除法与浮点除法、类型转换 |
| 3 | `Exercise03SwapVariables.cs` | 交换两个变量 | 赋值、临时变量 |
| 4 | `Exercise04OddOrEven.cs` | 判断奇数还是偶数 | `if/else`、`%`、比较运算 |
| 5 | `Exercise05LargestOfThree.cs` | 三个数中的最大值和最小值 | 比较运算、逻辑运算 `&&` |
| 6 | `Exercise06FizzBuzz.cs` | FizzBuzz | `for` 循环、`%`、`if/else if`、`StringBuilder` |
| 7 | `Exercise07ReverseString.cs` | 反转字符串 | 字符数组、倒序循环、`StringBuilder` |
| 8 | `HotAndColdGame.cs` | 冷热游戏（控制台练习） | `Random`、`while` 循环、`Mathf.Abs`、状态字段 |

---

## 1. 变量与字符串

**任务** —— 声明几个变量（姓名、年龄、身高），把它们拼成一句话输出到控制台。

**解法** —— 文本用 `string` 保存，整数用 `int`、小数用 `float` 保存，
最后用字符串插值 `$""` 把它们拼成一句话。

**知识点** —— 变量声明、基本数据类型、字符串插值、`Debug.Log`。

```csharp
using UnityEngine;

/// <summary>
/// 练习 1：变量与字符串
///
/// 任务 —— 声明几个变量（姓名、年龄、身高），把它们拼成一句话输出到控制台。
///
/// 解法 —— 文本用 string 保存，整数用 int、小数用 float 保存，
///         最后用字符串插值 $"" 把它们拼成一句话。
///
/// 知识点 —— 变量声明、基本数据类型、字符串插值、Debug.Log。
/// </summary>
public static class Exercise01Variables
{
    /// <summary>跑一遍这个练习，并把拼出来的句子返回。</summary>
    public static string Run()
    {
        string name = "小明";
        int age = 18;
        float height = 1.75f;
        bool isStudent = true;

        string message = $"我叫{name}，今年{age}岁，身高{height}米。";
        message += isStudent ? " 我还是一名学生。" : " 我已经工作了。";

        Debug.Log(message);
        return message;
    }
}
```

## 2. 算术运算

**任务** —— 给两个数字，算出它们的和、差、积、商和余数。

**解法** —— 直接用 `+ - * / %` 五个运算符。注意 `int` 与 `int` 相除会丢掉小数部分，
要得到小数得先转成 `float`。

**知识点** —— 算术运算符、整数除法与浮点除法、类型转换。

```csharp
using UnityEngine;

/// <summary>
/// 练习 2：算术运算
///
/// 任务 —— 给两个数字，算出它们的和、差、积、商和余数。
///
/// 解法 —— 直接用 + - * / % 五个运算符。注意 int 与 int 相除会丢掉小数部分，
///         要得到小数得先转成 float。
///
/// 知识点 —— 算术运算符、整数除法与浮点除法、类型转换。
/// </summary>
public static class Exercise02Arithmetic
{
    /// <summary>跑一遍这个练习，返回算出来的结果。</summary>
    public static string Run()
    {
        int a = 17;
        int b = 5;

        int sum = a + b;
        int difference = a - b;
        int product = a * b;
        float quotient = (float)a / b;      // 不转 float 的话 17 / 5 会变成 3
        int remainder = a % b;

        string message = $"{a} 和 {b}：和={sum}，差={difference}，积={product}，商={quotient}，余数={remainder}";
        Debug.Log(message);
        return message;
    }
}
```

## 3. 交换两个变量

**任务** —— 不借助第三个容器，交换两个变量里保存的值。

**解法** —— 用一个临时变量把 `a` 存下来：先把 `b` 写进 `a`，再把临时变量写进 `b`。

**知识点** —— 变量赋值、临时变量、值的拷贝。

```csharp
using UnityEngine;

/// <summary>
/// 练习 3：交换两个变量
///
/// 任务 —— 不借助第三个容器，交换两个变量里保存的值。
///
/// 解法 —— 用一个临时变量把 a 存下来：先把 b 写进 a，再把临时变量写进 b。
///
/// 知识点 —— 变量赋值、临时变量、值的拷贝。
/// </summary>
public static class Exercise03SwapVariables
{
    /// <summary>交换两个数并返回交换之后的结果。</summary>
    public static string Swap(int a, int b)
    {
        int temp = a;
        a = b;
        b = temp;

        string message = $"交换之后：a={a}，b={b}";
        Debug.Log(message);
        return message;
    }

    /// <summary>跑一遍这个练习。</summary>
    public static string Run()
    {
        return Swap(3, 8);
    }
}
```

## 4. 判断奇数还是偶数

**任务** —— 判断一个整数是奇数还是偶数。

**解法** —— 用 `%` 取余：余数为 0 就是偶数，否则是奇数。

**知识点** —— `if/else` 条件分支、`%` 运算符、比较运算。

```csharp
using UnityEngine;

/// <summary>
/// 练习 4：判断奇数还是偶数
///
/// 任务 —— 判断一个整数是奇数还是偶数。
///
/// 解法 —— 用 % 取余：余数为 0 就是偶数，否则是奇数。
///
/// 知识点 —— if/else 条件分支、% 运算符、比较运算。
/// </summary>
public static class Exercise04OddOrEven
{
    /// <summary>判断一个数字是奇数还是偶数，返回描述文字。</summary>
    public static string Describe(int number)
    {
        if (number % 2 == 0)
        {
            return number + " 是偶数";
        }

        return number + " 是奇数";
    }

    /// <summary>跑一遍这个练习。</summary>
    public static string Run()
    {
        string message = Describe(7) + "，" + Describe(12);
        Debug.Log(message);
        return message;
    }
}
```

## 5. 三个数中的最大值和最小值

**任务** —— 找出三个数字里最大的和最小的那个。

**解法** —— 两两比较，用 `&&` 把条件串起来：`a` 最大要同时比过 `b` 和 `c`。

**知识点** —— 比较运算符、逻辑运算符 `&&`、`if/else`。

```csharp
using UnityEngine;

/// <summary>
/// 练习 5：三个数中的最大值和最小值
///
/// 任务 —— 找出三个数字里最大的和最小的那个。
///
/// 解法 —— 两两比较，用 && 把条件串起来：a 最大要同时比过 b 和 c。
///
/// 知识点 —— 比较运算符、逻辑运算符 &&、if/else。
/// </summary>
public static class Exercise05LargestOfThree
{
    /// <summary>返回三个数里最大的那个。</summary>
    public static int Largest(int a, int b, int c)
    {
        if (a >= b && a >= c)
        {
            return a;
        }

        if (b >= a && b >= c)
        {
            return b;
        }

        return c;
    }

    /// <summary>返回三个数里最小的那个。</summary>
    public static int Smallest(int a, int b, int c)
    {
        if (a <= b && a <= c)
        {
            return a;
        }

        if (b <= a && b <= c)
        {
            return b;
        }

        return c;
    }

    /// <summary>跑一遍这个练习。</summary>
    public static string Run()
    {
        string message = $"3, 9, 4：最大={Largest(3, 9, 4)}，最小={Smallest(3, 9, 4)}";
        Debug.Log(message);
        return message;
    }
}
```

## 6. FizzBuzz

**任务** —— 从 1 数到 100：3 的倍数打印 `Fizz`，5 的倍数打印 `Buzz`，
同时是 3 和 5 的倍数打印 `FizzBuzz`，其它数字直接打印数字本身。

**解法** —— `for` 循环遍历，用 `%` 判断倍数；先判断 `FizzBuzz`，
这样两个条件都满足时不会走进第一个分支。

**知识点** —— `for` 循环、`%` 运算符、`if/else if` 分支、字符串拼接。

```csharp
using System.Text;
using UnityEngine;

/// <summary>
/// 练习 6：FizzBuzz
///
/// 任务 —— 从 1 数到 100：3 的倍数打印 Fizz，5 的倍数打印 Buzz，
///         同时是 3 和 5 的倍数打印 FizzBuzz，其它数字直接打印数字本身。
///
/// 解法 —— for 循环遍历，用 % 判断倍数；先判断 FizzBuzz，
///         这样两个条件都满足时不会走进第一个分支。
///
/// 知识点 —— for 循环、% 运算符、if/else if 分支、字符串拼接。
/// </summary>
public static class Exercise06FizzBuzz
{
    /// <summary>生成 1~count 的 FizzBuzz 文字。</summary>
    public static string Build(int count)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 1; i <= count; i++)
        {
            if (i % 3 == 0 && i % 5 == 0)
            {
                builder.Append("FizzBuzz");
            }
            else if (i % 3 == 0)
            {
                builder.Append("Fizz");
            }
            else if (i % 5 == 0)
            {
                builder.Append("Buzz");
            }
            else
            {
                builder.Append(i);
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>跑一遍这个练习，只把前 15 行打印到控制台。</summary>
    public static string Run()
    {
        string message = Build(100);
        string[] lines = message.Split('\n');
        Debug.Log(string.Join("\n", lines, 0, 15));
        return message;
    }
}
```

## 7. 反转字符串

**任务** —— 把一个字符串倒过来输出。

**解法** —— 先用 `ToCharArray()` 把字符串变成字符数组，再从后往前遍历，
把字符一个一个拼回新的字符串。

**知识点** —— 字符串与字符数组、`for` 循环、`StringBuilder`。

```csharp
using System.Text;
using UnityEngine;

/// <summary>
/// 练习 7：反转字符串
///
/// 任务 —— 把一个字符串倒过来输出。
///
/// 解法 —— 先用 ToCharArray() 把字符串变成字符数组，再从后往前遍历，
///         把字符一个一个拼回新的字符串。
///
/// 知识点 —— 字符串与字符数组、for 循环、StringBuilder。
/// </summary>
public static class Exercise07ReverseString
{
    /// <summary>返回反转之后的字符串。</summary>
    public static string Reverse(string text)
    {
        char[] letters = text.ToCharArray();
        StringBuilder builder = new StringBuilder(letters.Length);

        for (int i = letters.Length - 1; i >= 0; i--)
        {
            builder.Append(letters[i]);
        }

        return builder.ToString();
    }

    /// <summary>跑一遍这个练习。</summary>
    public static string Run()
    {
        string message = "Raining Apocalypse 反转后是：" + Reverse("Raining Apocalypse");
        Debug.Log(message);
        return message;
    }
}
```

## 8. 冷热游戏（控制台练习）

**任务** —— 程序随机想一个 0~100 的数字，玩家反复猜，
程序只回答「更热了」（猜得更近）还是「更冷了」（猜得更远）。

**解法** —— 记住上一次猜测和答案之间的距离：这次的距离比上次小就是更热，
变大就是更冷；猜中时游戏结束，可以重新开始一局。

**知识点** —— `Random` 随机数、`while` 循环、`Mathf.Abs` 求绝对值、
`if/else` 条件分支、把状态保存在字段里。

```csharp
using System;
using UnityEngine;

/// <summary>
/// 冷热游戏（课程里的控制台练习）
///
/// 任务 —— 程序随机想一个 0~100 的数字，玩家反复猜，
///         程序只回答「更热了」（猜得更近）还是「更冷了」（猜得更远）。
///
/// 解法 —— 记住上一次猜测和答案之间的距离：这次的距离比上次小就是更热，
///         变大就是更冷；猜中时游戏结束，可以重新开始一局。
///
/// 知识点 —— Random 随机数、while 循环、Mathf.Abs 求绝对值、
///         if/else 条件分支、把状态保存在字段里。
/// </summary>
public static class HotAndColdGame
{
    /// <summary>猜测的最小值。</summary>
    public const int MinNumber = 0;

    /// <summary>猜测的最大值。</summary>
    public const int MaxNumber = 100;

    /// <summary>这一局随机出来的答案。</summary>
    public static int SecretNumber { get; private set; }

    /// <summary>上一次猜的数字。</summary>
    public static int LastGuess { get; private set; }

    /// <summary>是否已经猜过一次。</summary>
    public static bool HasGuessed { get; private set; }

    /// <summary>这一局是不是已经结束（猜中或者重新开始）。</summary>
    public static bool IsOver { get; private set; }

    /// <summary>开始一局新的游戏，重新随机一个答案。</summary>
    public static void StartNewGame()
    {
        SecretNumber = UnityEngine.Random.Range(MinNumber, MaxNumber + 1);
        LastGuess = 0;
        HasGuessed = false;
        IsOver = false;
    }

    /// <summary>
    /// 提交一次猜测，返回给玩家的反馈：
    /// 「猜中了！」「更热了！」「更冷了！」「一样热！」。
    /// </summary>
    public static string Guess(int guess)
    {
        if (IsOver)
        {
            return "这一局已经结束了，请先开始新的一局。";
        }

        int distance = Mathf.Abs(guess - SecretNumber);

        if (distance == 0)
        {
            LastGuess = guess;
            HasGuessed = true;
            IsOver = true;
            return "猜中了！";
        }

        string feedback;
        if (!HasGuessed)
        {
            feedback = "第一次猜测，已经记下了这个距离。";
        }
        else
        {
            int lastDistance = Mathf.Abs(LastGuess - SecretNumber);
            if (distance < lastDistance)
            {
                feedback = "更热了！";
            }
            else if (distance > lastDistance)
            {
                feedback = "更冷了！";
            }
            else
            {
                feedback = "一样热！";
            }
        }

        LastGuess = guess;
        HasGuessed = true;
        return feedback;
    }

    /// <summary>
    /// 在 Unity 的控制台里玩一局：反复读入玩家输入，直到猜中为止。
    /// 这个方法只在编辑器里手动调用，不会影响游戏本身。
    /// </summary>
    public static void PlayInConsole()
    {
        StartNewGame();
        Debug.Log($"我想好了一个 {MinNumber}~{MaxNumber} 之间的数字，猜猜是多少？");

        bool playing = true;
        while (playing)
        {
            string input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
            {
                Debug.Log("没有读到输入，这一局结束。");
                return;
            }

            int guess;
            if (!int.TryParse(input, out guess))
            {
                Debug.Log("请输入一个整数。");
                continue;
            }

            string feedback = Guess(guess);
            Debug.Log($"你猜的是 {guess}：{feedback}");
            playing = !IsOver;
        }
    }

    /// <summary>跑一遍这个练习，演示「更热」和「猜中」两种反馈。</summary>
    public static string Run()
    {
        StartNewGame();

        // 第一次猜一个离答案很远的数字，第二次猜一个近的，第三次直接猜中。
        int far = SecretNumber > 50 ? SecretNumber - 30 : SecretNumber + 30;
        int near = SecretNumber > 50 ? SecretNumber - 10 : SecretNumber + 10;

        string first = Guess(far);
        string second = Guess(near);
        string third = Guess(SecretNumber);

        string message = $"答案是 {SecretNumber}：先猜 {far} 得到「{first}」，" +
                         $"再猜 {near} 得到「{second}」，最后猜中得到「{third}」";
        Debug.Log(message);
        return message;
    }
}
```

## 与游戏的连接

这些练习是独立文件，不参与游戏流程，因此：

- 它们全部是普通 `static class`，不挂在任何 GameObject 上，不会影响运行时性能；
- 它们只依赖 `UnityEngine`（`Debug.Log`、`Mathf`、`Random`），没有引入任何新包；
- 旧版 Input Manager 与之无关，练习里没有输入读取；
- `RainingApocalypseProbe` 会调用 `HotAndColdGame` 跑一遍，确认这些代码真的能编译并且给出正确的冷热反馈。
