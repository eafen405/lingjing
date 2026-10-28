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
