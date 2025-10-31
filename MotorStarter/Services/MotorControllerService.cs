using System.Collections.Concurrent;
using System.Linq;
using MotorStarter.Models;

namespace MotorStarter.Services;

public class MotorControllerService : IMotorControllerService
{
    private static readonly Dictionary<MotorActionType, string> CommandMap = new()
    {
        [MotorActionType.Start] = "555",
        [MotorActionType.Stop] = "000",
        [MotorActionType.Status] = "888"
    };

    private readonly ISmsService _smsService;
    private readonly IMotorLogService _logService;
    private readonly IUserService _userService;
    private readonly ITimerScheduler _timerScheduler;
    private readonly ConcurrentQueue<MotorActionLog> _awaitingResponses = new();
    private readonly List<ScheduledMotorAction> _scheduled = new();
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private bool _initialized;
    private string _lastKnownStatus = "Unknown";

    public event EventHandler<MotorActionLog>? ActionProcessed;

    public IReadOnlyCollection<ScheduledMotorAction> ScheduledActions
    {
        get
        {
            lock (_scheduled)
            {
                return _scheduled.Select(CloneScheduled).ToList();
            }
        }
    }

    public MotorControllerService(ISmsService smsService, IMotorLogService logService, IUserService userService, ITimerScheduler timerScheduler)
    {
        _smsService = smsService;
        _logService = logService;
        _userService = userService;
        _timerScheduler = timerScheduler;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _actionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _smsService.EnsurePermissionsAsync(cancellationToken).ConfigureAwait(false);
            await _logService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _userService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            await _timerScheduler.InitializeAsync(cancellationToken).ConfigureAwait(false);

            _smsService.MessageReceived += HandleSmsReceived;
            _timerScheduler.TimerTriggered += HandleTimerTriggered;

            _initialized = true;
        }
        finally
        {
            _actionLock.Release();
        }
    }

    public async Task<MotorActionLog> StartMotorAsync(User requestedBy, CancellationToken cancellationToken = default)
    {
        ValidateUser(requestedBy, MotorActionType.Start);
        return await ExecuteCommandAsync(requestedBy, MotorActionType.Start, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MotorActionLog> StopMotorAsync(User requestedBy, CancellationToken cancellationToken = default)
    {
        ValidateUser(requestedBy, MotorActionType.Stop);
        return await ExecuteCommandAsync(requestedBy, MotorActionType.Stop, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MotorActionLog> RequestStatusAsync(User requestedBy, CancellationToken cancellationToken = default)
    {
        ValidateUser(requestedBy, MotorActionType.Status);
        return await ExecuteCommandAsync(requestedBy, MotorActionType.Status, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MotorActionLog> ScheduleActionAsync(User requestedBy, ScheduledMotorAction action, CancellationToken cancellationToken = default)
    {
        if (action.ActionType is not MotorActionType.Start and not MotorActionType.Stop)
        {
            throw new ArgumentException("Only start or stop actions can be scheduled.", nameof(action));
        }

        ValidateUser(requestedBy, action.ActionType);
        action.RequestedBy = requestedBy.Name;
        action.Executed = false;

        await _timerScheduler.ScheduleAsync(action, cancellationToken).ConfigureAwait(false);
        lock (_scheduled)
        {
            _scheduled.Add(CloneScheduled(action));
        }

        var log = new MotorActionLog
        {
            ActionType = action.ActionType == MotorActionType.Start ? MotorActionType.TimerStart : MotorActionType.TimerStop,
            Message = $"Scheduled {action.ActionType} at {action.ExecuteAt:G}",
            Timestamp = DateTime.UtcNow,
            UserName = requestedBy.Name
        };

        await _logService.SaveLogAsync(log, cancellationToken).ConfigureAwait(false);
        ActionProcessed?.Invoke(this, log);

        return log;
    }

    private async Task<MotorActionLog> ExecuteCommandAsync(User requestedBy, MotorActionType actionType, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        if (!CommandMap.TryGetValue(actionType, out var command))
        {
            throw new InvalidOperationException($"No SMS command mapping configured for action {actionType}.");
        }

        await _actionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _smsService.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);

            var log = new MotorActionLog
            {
                ActionType = actionType,
                Message = command,
                Timestamp = DateTime.UtcNow,
                UserName = requestedBy.Name
            };

            await _logService.SaveLogAsync(log, cancellationToken).ConfigureAwait(false);
            _awaitingResponses.Enqueue(log);
            ActionProcessed?.Invoke(this, log);
            return log;
        }
        finally
        {
            _actionLock.Release();
        }
    }

    private void ValidateUser(User user, MotorActionType action)
    {
        if (user.Role == UserRole.Master)
        {
            return;
        }

        var allowed = action switch
        {
            MotorActionType.Start => user.CanStart,
            MotorActionType.Stop => user.CanStop,
            MotorActionType.Status => user.CanViewStatus,
            _ => false
        };

        if (!allowed)
        {
            throw new InvalidOperationException($"User {user.Name} is not permitted to perform {action} operations.");
        }
    }

    private async void HandleSmsReceived(object? sender, SmsMessageEventArgs e)
    {
        try
        {
            if (_awaitingResponses.TryDequeue(out var pending))
            {
                pending.Response = e.Message;
                pending.Timestamp = DateTime.UtcNow;
                await _logService.UpdateLogAsync(pending).ConfigureAwait(false);
                _lastKnownStatus = InterpretStatus(e.Message) ?? _lastKnownStatus;
                ActionProcessed?.Invoke(this, pending);
            }
            else
            {
                var log = new MotorActionLog
                {
                    ActionType = MotorActionType.Status,
                    Message = "Controller response",
                    Response = e.Message,
                    Timestamp = DateTime.UtcNow,
                    UserName = "Controller"
                };

                await _logService.SaveLogAsync(log).ConfigureAwait(false);
                _lastKnownStatus = InterpretStatus(e.Message) ?? _lastKnownStatus;
                ActionProcessed?.Invoke(this, log);
            }
        }
        catch (Exception ex)
        {
            var log = new MotorActionLog
            {
                ActionType = MotorActionType.Error,
                Message = "SMS processing error",
                Response = ex.Message,
                Timestamp = DateTime.UtcNow,
                UserName = "System"
            };

            await _logService.SaveLogAsync(log).ConfigureAwait(false);
            ActionProcessed?.Invoke(this, log);
        }
    }

    private async void HandleTimerTriggered(object? sender, ScheduledMotorAction action)
    {
        try
        {
            var users = await _userService.GetUsersAsync().ConfigureAwait(false);
            var user = users.FirstOrDefault(u => string.Equals(u.Name, action.RequestedBy, StringComparison.OrdinalIgnoreCase))
                ?? await _userService.GetMasterUserAsync().ConfigureAwait(false);

            if (action.ActionType == MotorActionType.Start)
            {
                await StartMotorAsync(user).ConfigureAwait(false);
            }
            else
            {
                await StopMotorAsync(user).ConfigureAwait(false);
            }

            lock (_scheduled)
            {
                var tracked = _scheduled.FirstOrDefault(x => x.Id == action.Id);
                if (tracked is not null)
                {
                    tracked.Executed = true;
                }
            }
        }
        catch (Exception ex)
        {
            var log = new MotorActionLog
            {
                ActionType = MotorActionType.Error,
                Message = $"Failed to run scheduled action {action.Id}",
                Response = ex.Message,
                Timestamp = DateTime.UtcNow,
                UserName = action.RequestedBy
            };

            await _logService.SaveLogAsync(log).ConfigureAwait(false);
            ActionProcessed?.Invoke(this, log);
        }
    }

    private static string? InterpretStatus(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalized = message.ToLowerInvariant();
        if (normalized.Contains("start") || normalized.Contains("on"))
        {
            return "Motor Started";
        }

        if (normalized.Contains("stop") || normalized.Contains("off"))
        {
            return "Motor Stopped";
        }

        if (normalized.Contains("fault") || normalized.Contains("error"))
        {
            return "Error";
        }

        return null;
    }

    private static ScheduledMotorAction CloneScheduled(ScheduledMotorAction action) => new()
    {
        Id = action.Id,
        ActionType = action.ActionType,
        ExecuteAt = action.ExecuteAt,
        RequestedBy = action.RequestedBy,
        Executed = action.Executed
    };
}
