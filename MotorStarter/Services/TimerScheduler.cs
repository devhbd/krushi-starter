using System.Linq;
using MotorStarter.Models;

namespace MotorStarter.Services;

public class TimerScheduler : ITimerScheduler
{
    private readonly List<ScheduledMotorAction> _pending = new();
    private int _nextId = 1;
    private readonly object _lock = new();

    public event EventHandler<ScheduledMotorAction>? TimerTriggered;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<ScheduledMotorAction> ScheduleAsync(ScheduledMotorAction action, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            action.Id = _nextId++;
            _pending.Add(action);
        }

        _ = MonitorActionAsync(action, cancellationToken);
        return Task.FromResult(action);
    }

    public Task CancelAsync(int actionId, CancellationToken cancellationToken = default)
    {
        ScheduledMotorAction? existing = null;
        lock (_lock)
        {
            existing = _pending.FirstOrDefault(x => x.Id == actionId);
            if (existing is not null)
            {
                _pending.Remove(existing);
            }
        }

        if (existing is not null)
        {
            existing.Executed = true;
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<ScheduledMotorAction> GetPendingActions()
    {
        lock (_lock)
        {
            return _pending.Select(x => new ScheduledMotorAction
            {
                Id = x.Id,
                ActionType = x.ActionType,
                ExecuteAt = x.ExecuteAt,
                RequestedBy = x.RequestedBy,
                Executed = x.Executed
            }).ToList();
        }
    }

    private async Task MonitorActionAsync(ScheduledMotorAction action, CancellationToken cancellationToken)
    {
        try
        {
            var delay = action.ExecuteAt - DateTime.Now;
            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                if (action.Executed || !_pending.Contains(action))
                {
                    return;
                }

                action.Executed = true;
                _pending.Remove(action);
            }

            TimerTriggered?.Invoke(this, action);
        }
        catch (TaskCanceledException)
        {
            // ignore cancellation
        }
    }
}
