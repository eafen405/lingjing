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
