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
