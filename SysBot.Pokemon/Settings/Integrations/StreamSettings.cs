using PKHeX.Core;
using SysBot.Base;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public class StreamSettings
{
    private const string Operation = "操作设置";

    public override string ToString() => "流媒体设置";
    public static Action<PKM, string>? CreateSpriteFile { get; set; }

    [Category(Operation), Description("生成流媒体资源；关闭将阻止生成资源。")]
    public bool CreateAssets { get; set; }

    [Category(Operation), Description("生成交易开始详情，指示机器人正在与谁交易。")]
    public bool CreateTradeStart { get; set; } = true;

    [Category(Operation), Description("生成交易开始详情，指示机器人正在交易什么。")]
    public bool CreateTradeStartSprite { get; set; } = true;

    [Category(Operation), Description("显示当前交易详情的格式。{0} = ID, {1} = 用户")]
    public string TrainerTradeStart { get; set; } = "(ID {0}) {1}";

    // 预备队列

    [Category(Operation), Description("生成当前预备队列中的人员列表。")]
    public bool CreateOnDeck { get; set; } = true;

    [Category(Operation), Description("在预备队列列表中显示的用户数量。")]
    public int OnDeckTake { get; set; } = 5;

    [Category(Operation), Description("在顶部跳过的预备队列用户数量。如果要隐藏正在处理的用户，请将此设置为您的控制台数量。")]
    public int OnDeckSkip { get; set; }

    [Category(Operation), Description("分隔预备队列列表用户的分隔符。")]
    public string OnDeckSeparator { get; set; } = "\n";

    [Category(Operation), Description("显示预备队列列表用户的格式。{0} = ID, {3} = 用户")]
    public string OnDeckFormat { get; set; } = "(ID {0}) - {3}";

    // 预备队列 2

    [Category(Operation), Description("生成当前预备队列 #2 中的人员列表。")]
    public bool CreateOnDeck2 { get; set; } = true;

    [Category(Operation), Description("在预备队列 #2 列表中显示的用户数量。")]
    public int OnDeckTake2 { get; set; } = 5;

    [Category(Operation), Description("在顶部跳过的预备队列 #2 用户数量。如果要隐藏正在处理的用户，请将此设置为您的控制台数量。")]
    public int OnDeckSkip2 { get; set; }

    [Category(Operation), Description("分隔预备队列 #2 列表用户的分隔符。")]
    public string OnDeckSeparator2 { get; set; } = "\n";

    [Category(Operation), Description("显示预备队列 #2 列表用户的格式。{0} = ID, {3} = 用户")]
    public string OnDeckFormat2 { get; set; } = "(ID {0}) - {3}";

    // 用户列表

    [Category(Operation), Description("生成当前正在交易的人员列表。")]
    public bool CreateUserList { get; set; } = true;

    [Category(Operation), Description("在列表中显示的用户数量。")]
    public int UserListTake { get; set; } = -1;

    [Category(Operation), Description("在顶部跳过的用户数量。如果要隐藏正在处理的用户，请将此设置为您的控制台数量。")]
    public int UserListSkip { get; set; }

    [Category(Operation), Description("分隔列表用户的分隔符。")]
    public string UserListSeparator { get; set; } = ", ";

    [Category(Operation), Description("显示列表用户的格式。{0} = ID, {3} = 用户")]
    public string UserListFormat { get; set; } = "(ID {0}) - {3}";

    // 交易代码块

    [Category(Operation), Description("如果 TradeBlockFile 存在，则复制它，否则复制占位符图像。")]
    public bool CopyImageFile { get; set; } = true;

    [Category(Operation), Description("输入交易代码时要复制的图像源文件名。如果留空，将创建占位符图像。")]
    public string TradeBlockFile { get; set; } = string.Empty;

    [Category(Operation), Description("链接代码阻止图像的目标文件名。{0} 将被本地 IP 地址替换。")]
    public string TradeBlockFormat { get; set; } = "block_{0}.png";

    // 等待时间

    [Category(Operation), Description("创建一个文件，列出最近出队的用户已等待的时间量。")]
    public bool CreateWaitedTime { get; set; } = true;

    [Category(Operation), Description("显示最近出队用户的等待时间的格式。")]
    public string WaitedTimeFormat { get; set; } = @"hh\:mm\:ss";

    // 预估时间

    [Category(Operation), Description("创建一个文件，列出用户如果加入队列将需要等待的预估时间量。")]
    public bool CreateEstimatedTime { get; set; } = true;

    [Category(Operation), Description("显示预估等待时间的格式。")]
    public string EstimatedTimeFormat { get; set; } = "Estimated time: {0:F1} minutes";

    [Category(Operation), Description("显示预估等待时间戳的格式。")]
    public string EstimatedFulfillmentFormat { get; set; } = @"hh\:mm\:ss";

    // 队列中的用户

    [Category(Operation), Description("创建一个指示队列中用户数量的文件。")]
    public bool CreateUsersInQueue { get; set; } = true;

    [Category(Operation), Description("显示队列中用户的格式。{0} = 数量")]
    public string UsersInQueueFormat { get; set; } = "Users in Queue: {0}";

    // 完成的交易

    [Category(Operation), Description("在新交易开始时创建一个指示已完成交易数量的文件。")]
    public bool CreateCompletedTrades { get; set; } = true;

    [Category(Operation), Description("显示已完成交易的格式。{0} = 数量")]
    public string CompletedTradesFormat { get; set; } = "Completed Trades: {0}";

    public void StartTrade<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail, PokeTradeHub<T> hub) where T : PKM, new()
    {
        if (!CreateAssets)
            return;

        try
        {
            if (CreateTradeStart)
                GenerateBotConnection(b, detail);
            if (CreateWaitedTime)
                GenerateWaitedTime(detail.Time);
            if (CreateEstimatedTime)
                GenerateEstimatedTime(hub);
            if (CreateUsersInQueue)
                GenerateUsersInQueue(hub.Queues.Info.Count);
            if (CreateOnDeck)
                GenerateOnDeck(hub);
            if (CreateOnDeck2)
                GenerateOnDeck2(hub);
            if (CreateUserList)
                GenerateUserList(hub);
            if (CreateCompletedTrades)
                GenerateCompletedTrades(hub);
            if (CreateTradeStartSprite)
                GenerateBotSprite(b, detail);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    public void IdleAssets(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            var files = Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                if (file.Contains(b.Connection.Name))
                    File.Delete(file);
            }

            if (CreateWaitedTime)
                File.WriteAllText("waited.txt", "00:00:00");
            if (CreateEstimatedTime)
            {
                File.WriteAllText("estimatedTime.txt", "Estimated time: 0 minutes");
                File.WriteAllText("estimatedTimestamp.txt", "");
            }
            if (CreateOnDeck)
                File.WriteAllText("ondeck.txt", "Waiting...");
            if (CreateOnDeck2)
                File.WriteAllText("ondeck2.txt", "Queue is empty!");
            if (CreateUserList)
                File.WriteAllText("users.txt", "None");
            if (CreateUsersInQueue)
                File.WriteAllText("queuecount.txt", "Users in Queue: 0");
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    private void GenerateUsersInQueue(int count)
    {
        var value = string.Format(UsersInQueueFormat, count);
        File.WriteAllText("queuecount.txt", value);
    }

    private void GenerateWaitedTime(DateTime time)
    {
        var now = DateTime.Now;
        var difference = now - time;
        var value = difference.ToString(WaitedTimeFormat);
        File.WriteAllText("waited.txt", value);
    }

    private void GenerateEstimatedTime<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var count = hub.Queues.Info.Count;
        var estimate = hub.Config.Queues.EstimateDelay(count, hub.Bots.Count);

        // 分钟
        var wait = string.Format(EstimatedTimeFormat, estimate);
        File.WriteAllText("estimatedTime.txt", wait);

        // 预计在此时间完成
        var now = DateTime.Now;
        var difference = now.AddMinutes(estimate);
        var date = difference.ToString(EstimatedFulfillmentFormat);
        File.WriteAllText("estimatedTimestamp.txt", date);
    }

    public void StartEnterCode(PokeRoutineExecutorBase b)
    {
        if (!CreateAssets)
            return;

        try
        {
            var file = GetBlockFileName(b);
            if (CopyImageFile && File.Exists(TradeBlockFile))
                File.Copy(TradeBlockFile, file);
            else
                File.WriteAllBytes(file, BlackPixel);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    private static readonly byte[] BlackPixel = // 1x1 黑色像素
    [
        0x42, 0x4D, 0x3A, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x28, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x00, 0x01, 0x00, 0x18, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
    ];

    public void EndEnterCode(PokeRoutineExecutorBase b)
    {
        try
        {
            var file = GetBlockFileName(b);
            if (File.Exists(file))
                File.Delete(file);
        }
        catch (Exception e)
        {
            LogUtil.LogError(e.Message, nameof(StreamSettings));
        }
    }

    private string GetBlockFileName(PokeRoutineExecutorBase b) => string.Format(TradeBlockFormat, b.Connection.Name);

    private void GenerateBotConnection<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var file = b.Connection.Name;
        var name = string.Format(TrainerTradeStart, detail.ID, detail.Trainer.TrainerName, (Species)detail.TradeData.Species);
        File.WriteAllText($"{file}.txt", name);
    }

    private static void GenerateBotSprite<T>(PokeRoutineExecutorBase b, PokeTradeDetail<T> detail) where T : PKM, new()
    {
        var func = CreateSpriteFile;
        if (func == null)
            return;
        var file = b.Connection.Name;
        var pk = detail.TradeData;
        func.Invoke(pk, $"sprite_{file}.png");
    }

    private void GenerateOnDeck<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat);
        ondeck = ondeck.Skip(OnDeckSkip).Take(OnDeckTake); // 过滤
        File.WriteAllText("ondeck.txt", string.Join(OnDeckSeparator, ondeck));
    }

    private void GenerateOnDeck2<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var ondeck = hub.Queues.Info.GetUserList(OnDeckFormat2);
        ondeck = ondeck.Skip(OnDeckSkip2).Take(OnDeckTake2); // 过滤
        File.WriteAllText("ondeck2.txt", string.Join(OnDeckSeparator2, ondeck));
    }

    private void GenerateUserList<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var users = hub.Queues.Info.GetUserList(UserListFormat);
        users = users.Skip(UserListSkip);
        if (UserListTake > 0)
            users = users.Take(UserListTake); // 过滤
        File.WriteAllText("users.txt", string.Join(UserListSeparator, users));
    }

    private void GenerateCompletedTrades<T>(PokeTradeHub<T> hub) where T : PKM, new()
    {
        var msg = string.Format(CompletedTradesFormat, hub.Config.Trade.CompletedTrades);
        File.WriteAllText("completed.txt", msg);
    }
}
