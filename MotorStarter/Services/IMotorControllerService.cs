using MotorStarter.Models;

namespace MotorStarter.Services;

public interface IMotorControllerService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<MotorActionLog> StartMotorAsync(User requestedBy, CancellationToken cancellationToken = default);
    Task<MotorActionLog> StopMotorAsync(User requestedBy, CancellationToken cancellationToken = default);
    Task<MotorActionLog> RequestStatusAsync(User requestedBy, CancellationToken cancellationToken = default);
    Task<MotorActionLog> ScheduleActionAsync(User requestedBy, ScheduledMotorAction action, CancellationToken cancellationToken = default);
    IReadOnlyCollection<ScheduledMotorAction> ScheduledActions { get; }
    event EventHandler<MotorActionLog>? ActionProcessed;
}
