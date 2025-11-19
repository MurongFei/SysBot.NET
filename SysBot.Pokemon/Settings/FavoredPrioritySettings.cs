using System;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class FavoredPrioritySettings : IFavoredCPQSetting
{
    private const string Operation = "操作设置";
    private const string Configure = "配置设置";
    public override string ToString() => "优先设置";

    // 我们希望允许主机给予优先待遇，同时仍然为没有优先权的用户提供服务。
    // 这些是我们允许的最小值。这些值为优先用户提供了公平的位置。
    private const int _mfi = 2;
    private const float _bmin = 1;
    private const float _bmax = 3;
    private const float _mexp = 0.5f;
    private const float _mmul = 0.1f;

    private int _minimumFreeAhead = _mfi;
    private float _bypassFactor = 1.5f;
    private float _exponent = 0.777f;
    private float _multiply = 0.5f;

    [Category(Operation), Description("确定优先用户的插入位置如何计算。\"None\"将阻止应用任何优先权。")]
    public FavoredMode Mode { get; set; }

    [Category(Configure), Description("插入到（非优先用户）^(指数) 个非优先用户之后。")]
    public float Exponent
    {
        get => _exponent;
        set => _exponent = Math.Max(_mexp, value);
    }

    [Category(Configure), Description("乘法：插入到（非优先用户）*(乘数) 个非优先用户之后。将其设置为0.2表示在20%的用户之后插入。")]
    public float Multiply
    {
        get => _multiply;
        set => _multiply = Math.Max(_mmul, value);
    }

    [Category(Configure), Description("不跳过的非优先用户数量。这只有在队列中有大量非优先用户时才强制执行。")]
    public int MinimumFreeAhead
    {
        get => _minimumFreeAhead;
        set => _minimumFreeAhead = Math.Max(_mfi, value);
    }

    [Category(Configure), Description("导致强制执行 {MinimumFreeAhead} 的队列中非优先用户的最小数量。当上述数字高于此值时，优先用户不会排在 {MinimumFreeAhead} 个非优先用户之前。")]
    public int MinimumFreeBypass => (int)Math.Ceiling(MinimumFreeAhead * MinimumFreeBypassFactor);

    [Category(Configure), Description("与 {MinimumFreeAhead} 相乘以确定 {MinimumFreeBypass} 值的标量。")]
    public float MinimumFreeBypassFactor
    {
        get => _bypassFactor;
        set => _bypassFactor = Math.Min(_bmax, Math.Max(_bmin, value));
    }
}
