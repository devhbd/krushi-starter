using MotorStarter.Models;

namespace MotorStarter.Services;

public interface IMotorLogService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MotorActionLog>> GetLogsAsync(int take = 100, CancellationToken cancellationToken = default);
    Task SaveLogAsync(MotorActionLog log, CancellationToken cancellationToken = default);
    Task UpdateLogAsync(MotorActionLog log, CancellationToken cancellationToken = default);
}
