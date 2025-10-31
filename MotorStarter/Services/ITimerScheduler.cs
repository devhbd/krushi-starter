using MotorStarter.Models;

namespace MotorStarter.Services;

public interface ITimerScheduler
{
    event EventHandler<ScheduledMotorAction>? TimerTriggered;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<ScheduledMotorAction> ScheduleAsync(ScheduledMotorAction action, CancellationToken cancellationToken = default);
    Task CancelAsync(int actionId, CancellationToken cancellationToken = default);
    IReadOnlyList<ScheduledMotorAction> GetPendingActions();
}
