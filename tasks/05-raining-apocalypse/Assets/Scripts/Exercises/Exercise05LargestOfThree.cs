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
