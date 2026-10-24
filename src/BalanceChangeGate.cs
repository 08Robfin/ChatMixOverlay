namespace ChatMixOverlay;

// Small movements must settle; larger deliberate adjustments remain immediate.
// Pure time-based logic keeps the hardware-noise behavior testable without UI timers.
public sealed class BalanceChangeGate
{
    public const double Threshold = 0.05; // 2.5% of the full -1..+1 bar.
    public const double ImmediateThreshold = 0.15;
    public const double SettleSeconds = 0.8;
    private const double StableTolerance = 0.012;
    private double? anchor, pendingSince;
    private double pendingBalance;

    public void Reset(double balance) { anchor = balance; pendingSince = null; }

    public double? RemainingDelay(double now) => pendingSince.HasValue
        ? Math.Max(.001, pendingSince.Value + SettleSeconds - now) : null;

    public bool ShouldTrigger(double balance, double now, bool alreadyVisible = false)
    {
        if (!anchor.HasValue) { Reset(balance); return false; }
        var difference = Math.Abs(balance - anchor.Value);
        if (difference + .000001 < Threshold) { pendingSince = null; return false; }
        if (alreadyVisible || difference + .000001 >= ImmediateThreshold)
        {
            Reset(balance); return true;
        }
        if (!pendingSince.HasValue || Math.Abs(balance - pendingBalance) > StableTolerance)
        {
            pendingSince = now; pendingBalance = balance;
        }
        if (now - pendingSince.Value + .000001 < SettleSeconds) return false;
        Reset(balance); return true;
    }
}
