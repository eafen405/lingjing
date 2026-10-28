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
