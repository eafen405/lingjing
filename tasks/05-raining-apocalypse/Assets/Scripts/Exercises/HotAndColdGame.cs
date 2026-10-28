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
