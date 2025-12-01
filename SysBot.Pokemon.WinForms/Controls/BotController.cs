using SysBot.Base;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms;

public partial class BotController : UserControl
{
    public PokeBotState State { get; private set; } = new();
    private IPokeBotRunner? Runner;
    public EventHandler? Remove;

    public BotController()
    {
        InitializeComponent();
        var opt = Enum.GetValues<BotControlCommand>();

        for (int i = 1; i < opt.Length; i++)
        {
            var cmd = opt[i];
            var item = new ToolStripMenuItem(GetCommandChinese(cmd));
            item.Click += (_, __) => SendCommand(cmd);

            RCMenu.Items.Add(item);
        }

        var remove = new ToolStripMenuItem("移除");
        remove.Click += (_, __) => TryRemove();
        RCMenu.Items.Add(remove);
        RCMenu.Opening += RcMenuOnOpening;

        var controls = Controls;
        foreach (var c in controls.OfType<Control>())
        {
            c.MouseEnter += BotController_MouseEnter;
            c.MouseLeave += BotController_MouseLeave;
        }
    }

    private void RcMenuOnOpening(object? sender, CancelEventArgs? e)
    {
        var bot = Runner?.GetBot(State);
        if (bot is null)
            return;

        foreach (var tsi in RCMenu.Items.OfType<ToolStripMenuItem>())
        {
            var text = tsi.Text;
            tsi.Enabled = Enum.TryParse(GetCommandEnglish(text), out BotControlCommand cmd)
                ? cmd.IsUsable(bot.IsRunning, bot.IsPaused)
                : !bot.IsRunning;
        }
    }

    public void Initialize(IPokeBotRunner runner, PokeBotState cfg)
    {
        Runner = runner;
        State = cfg;
        ReloadStatus();
        L_Description.Text = string.Empty;
    }

    public void ReloadStatus()
    {
        var bot = GetBot().Bot;
        L_Left.Text = $"{bot.Connection.Name}{Environment.NewLine}{State.InitialRoutine}";
    }

    private DateTime LastUpdateStatus = DateTime.Now;

    public void ReloadStatus(BotSource<PokeBotState> b)
    {
        ReloadStatus();
        var bot = b.Bot;
        L_Description.Text = $"[{bot.LastTime:hh:mm:ss}] {bot.Connection.Label}: {bot.LastLogged}";
        L_Left.Text = $"{bot.Connection.Name}{Environment.NewLine}{State.InitialRoutine}";

        var lastTime = bot.LastTime;
        if (!b.IsRunning)
        {
            PB_Lamp.BackColor = Color.Transparent;
            return;
        }
        if (!b.Bot.Connection.Connected)
        {
            PB_Lamp.BackColor = Color.Aqua;
            return;
        }

        var cfg = bot.Config;
        if (cfg is { CurrentRoutineType: PokeRoutineType.Idle, NextRoutineType: PokeRoutineType.Idle })
        {
            PB_Lamp.BackColor = Color.Yellow;
            return;
        }
        if (LastUpdateStatus == lastTime)
            return;

        // 基于时间的颜色衰减（从绿色开始）
        const int threshold = 100;
        Color good = Color.Green;
        Color bad = Color.Red;

        var delta = DateTime.Now - lastTime;
        var seconds = delta.Seconds;

        LastUpdateStatus = lastTime;
        if (seconds > 2 * threshold)
            return; // 此时已经改变

        if (seconds > threshold)
        {
            if (PB_Lamp.BackColor == bad)
                return; // 我们应该在改变时通知吗？
            PB_Lamp.BackColor = bad;
        }
        else
        {
            // 从绿色混合到红色，在接近饱和之前偏向绿色
            var factor = seconds / (double)threshold;
            var blend = Blend(bad, good, factor * factor);
            PB_Lamp.BackColor = blend;
        }
    }

    private static Color Blend(Color color, Color backColor, double amount)
    {
        byte r = (byte)((color.R * amount) + (backColor.R * (1 - amount)));
        byte g = (byte)((color.G * amount) + (backColor.G * (1 - amount)));
        byte b = (byte)((color.B * amount) + (backColor.B * (1 - amount)));
        return Color.FromArgb(r, g, b);
    }

    public void TryRemove()
    {
        var bot = GetBot();
        if (!Runner!.Config.SkipConsoleBotCreation)
            bot.Stop();
        Remove?.Invoke(this, EventArgs.Empty);
    }

    public void SendCommand(BotControlCommand cmd, bool echo = true)
    {
        if (Runner?.Config.SkipConsoleBotCreation != false)
        {
            LogUtil.LogError("由于 SkipConsoleBotCreation 已开启，未创建任何机器人！", "Hub");
            return;
        }
        var bot = GetBot();
        switch (cmd)
        {
            case BotControlCommand.Idle: bot.Pause(); break;
            case BotControlCommand.Start:
                Runner.InitializeStart();
                bot.Start(); break;
            case BotControlCommand.Stop: bot.Stop(); break;
            case BotControlCommand.Resume: bot.Resume(); break;
            case BotControlCommand.Restart:
                {
                    var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "确定要重新启动连接吗？");
                    if (prompt != DialogResult.Yes)
                        return;

                    Runner.InitializeStart();
                    bot.Restart();
                    break;
                }
            default:
                WinFormsUtil.Alert($"{cmd} 不是可以发送给机器人的命令。");
                return;
        }
        if (echo)
            EchoUtil.Echo($"{bot.Bot.Connection.Name} ({bot.Bot.Config.InitialRoutine}) 已收到 {GetCommandChinese(cmd)} 命令。");
    }

    private BotSource<PokeBotState> GetBot()
    {
        if (Runner == null)
            throw new ArgumentNullException(nameof(Runner));

        var bot = Runner.GetBot(State);
        if (bot == null)
            throw new ArgumentNullException(nameof(bot));
        return bot;
    }

#pragma warning disable WFO5001
    private void BotController_MouseEnter(object? sender, EventArgs e) => BackColor = Application.IsDarkModeEnabled ? Color.MidnightBlue : Color.LightSkyBlue;
#pragma warning restore WFO5001
    private void BotController_MouseLeave(object? sender, EventArgs e) => BackColor = Color.Transparent;

    public void ReadState()
    {
        var bot = GetBot();

        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() => ReloadStatus(bot)));
        }
        else
        {
            ReloadStatus(bot);
        }
    }

    /// <summary>
    /// 获取命令的中文描述
    /// </summary>
    private static string GetCommandChinese(BotControlCommand cmd)
    {
        return cmd switch
        {
            BotControlCommand.Start => "启动",
            BotControlCommand.Stop => "停止",
            BotControlCommand.Idle => "空闲",
            BotControlCommand.Resume => "恢复",
            BotControlCommand.Restart => "重启",
            _ => cmd.ToString()
        };
    }

    /// <summary>
    /// 将中文命令文本转换回英文枚举
    /// </summary>
    private static string GetCommandEnglish(string? chineseText)
    {
        if (string.IsNullOrEmpty(chineseText))
            return string.Empty;

        return chineseText switch
        {
            "启动" => "Start",
            "停止" => "Stop",
            "空闲" => "Idle",
            "恢复" => "Resume",
            "重启" => "Restart",
            _ => chineseText
        };
    }
}

public enum BotControlCommand
{
    None,
    Start,
    Stop,
    Idle,
    Resume,
    Restart,
}

public static class BotControlCommandExtensions
{
    public static bool IsUsable(this BotControlCommand cmd, bool running, bool paused)
    {
        return cmd switch
        {
            BotControlCommand.Start => !running,
            BotControlCommand.Stop => running,
            BotControlCommand.Idle => running && !paused,
            BotControlCommand.Resume => paused,
            BotControlCommand.Restart => true,
            _ => false,
        };
    }
}
