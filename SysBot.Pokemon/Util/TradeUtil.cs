using SysBot.Base;
using System;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon;

public static class TradeUtil
{
    public static int GetCodeDigit(int code, int c)
    {
        // 将code转为8位字符串，然后按位置取数字
        string codeStr = code.ToString("00000000");
        return int.Parse(codeStr[c].ToString());
    }

    public static IEnumerable<SwitchButton> GetPresses(int code)
    {
        var end = 1;
        for (int i = 0; i < 8; i++)
        {
            var key = GetCodeDigit(code, i);
            foreach (var k in MoveCursor(end, key))
                yield return k;
            yield return A;
            end = key;
        }
    }

    private static IEnumerable<SwitchButton> MoveCursor(int start, int dest)
    {
        if (start == dest)
            yield break;
        if (dest == 0)
        {
            // 修复数字0的处理逻辑
            int row = (start - 1) / 3;
            int movesDown = 3 - row; // 需要按下的次数

            for (int i = 0; i < movesDown; i++)
                yield return DDOWN;

            // 0在第二列，如果当前不在第二列需要水平移动
            int currentCol = (start - 1) % 3;
            if (currentCol < 1)  // 当前在第一列(0)，需要向右移动
                yield return DRIGHT;
            else if (currentCol > 1)  // 当前在第三列(2)，需要向左移动
                yield return DLEFT;

            yield break;
        }
        if (start == 0)
        {
            yield return DUP; // up
            start = 8;
        }

        foreach (var m in MoveSquare(start, dest))
            yield return m;
    }

    private static IEnumerable<SwitchButton> MoveSquare(int start, int dest)
    {
        int dindex = dest - 1;
        int cindex = start - 1;
        int dcol = dindex % 3;
        int ccol = cindex % 3;
        int drow = dindex / 3;
        int crow = cindex / 3;

        if (drow != crow)
        {
            var dir = drow > crow ? DDOWN : DUP;
            var count = Math.Abs(drow - crow);
            for (int i = 0; i < count; i++)
                yield return dir;
        }
        if (dcol != ccol)
        {
            var dir = dcol > ccol ? DRIGHT : DLEFT;
            var count = Math.Abs(dcol - ccol);
            for (int i = 0; i < count; i++)
                yield return dir;
        }
    }
}
