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
