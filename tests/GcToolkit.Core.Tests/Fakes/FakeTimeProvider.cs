namespace GcToolkit.Core.Tests.Fakes;

/// <summary>Manual clock: timers fire synchronously inside <see cref="Advance"/>.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public int ActiveTimerCount => _timers.Count(t => t.IsActive);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        FakeTimer timer = new(this, callback, state);
        _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        var target = _now + by;
        while (true)
        {
            var next = _timers.Where(t => t.IsActive && t.Due <= target).OrderBy(t => t.Due).FirstOrDefault();
            if (next is null)
            {
                break;
            }

            _now = next.Due;
            next.Fire();
        }

        _now = target;
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period;

        public bool IsActive { get; private set; }

        public DateTimeOffset Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            IsActive = dueTime != Timeout.InfiniteTimeSpan;
            Due = owner._now + (IsActive ? dueTime : TimeSpan.Zero);
            return true;
        }

        public void Fire()
        {
            if (_period > TimeSpan.Zero)
            {
                Due += _period;
            }
            else
            {
                IsActive = false;
            }

            callback(state);
        }

        public void Dispose() => IsActive = false;

        public ValueTask DisposeAsync()
        {
            IsActive = false;
            return ValueTask.CompletedTask;
        }
    }
}
