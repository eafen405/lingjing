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
