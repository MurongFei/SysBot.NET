using SysBot.Base;
using System;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    public static class TradeUtil
    {
        public static int GetCodeDigit(int code, int c)
        {
            for (int i = 7; i > c; i--)
                code /= 10;
            return code % 10;
        }

        public static IEnumerable<SwitchButton> GetPresses(int code)
        {
            // 确保代码为8位数
            string codeStr = code.ToString("D8");
            var end = 1; // 默认从数字1开始

            for (int i = 0; i < 8; i++)
            {
                int digit = int.Parse(codeStr[i].ToString());
                foreach (var button in MoveCursor(end, digit))
                    yield return button;

                yield return A; // 按下确认
                end = digit; // 更新当前位置
            }
        }

        private static IEnumerable<SwitchButton> MoveCursor(int start, int dest)
        {
            if (start == dest)
                yield break;

            // 处理目标为数字0的情况
            if (dest == 0)
            {
                int currentRow = (start - 1) / 3;
                for (int i = currentRow; i < 3; i++)
                    yield return DDOWN;
                yield break;
            }

            // 处理当前位置为数字0的情况
            if (start == 0)
            {
                yield return DUP;
                start = 8; // 移动到数字8的位置
            }

            // 正常网格移动逻辑
            foreach (var button in MoveSquare(start, dest))
                yield return button;
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
}
