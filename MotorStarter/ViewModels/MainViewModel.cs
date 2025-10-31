using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using MotorStarter.Models;
using MotorStarter.Services;

namespace MotorStarter.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IMotorControllerService _motorControllerService;
    private readonly IMotorLogService _motorLogService;
    private readonly IUserService _userService;
    private readonly ITimerScheduler _timerScheduler;
    private bool _isInitialized;
    private bool _isBusy;
    private User? _selectedUser;
    private string _currentStatus = "Unknown";
    private DateTime _scheduleDate = DateTime.Today;
    private TimeSpan _scheduleTime = TimeSpan.FromHours(6);
    private MotorActionType _selectedScheduleAction = MotorActionType.Start;

    public ObservableCollection<MotorActionLog> Logs { get; } = new();
    public ObservableCollection<User> Users { get; } = new();
    public IList<MotorActionType> ScheduleActions { get; } = new[] { MotorActionType.Start, MotorActionType.Stop };

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StatusCommand { get; }
    public ICommand ScheduleCommand { get; }
    public ICommand AddUserCommand { get; }
    public ICommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public string CurrentStatus
    {
        get => _currentStatus;
        set => SetProperty(ref _currentStatus, value);
    }

    public DateTime ScheduleDate
    {
        get => _scheduleDate;
        set => SetProperty(ref _scheduleDate, value);
    }

    public TimeSpan ScheduleTime
    {
        get => _scheduleTime;
        set => SetProperty(ref _scheduleTime, value);
    }

    public MotorActionType SelectedScheduleAction
    {
        get => _selectedScheduleAction;
        set => SetProperty(ref _selectedScheduleAction, value);
    }

    public MainViewModel(
        IMotorControllerService motorControllerService,
        IMotorLogService motorLogService,
        IUserService userService,
        ITimerScheduler timerScheduler)
    {
        _motorControllerService = motorControllerService;
        _motorLogService = motorLogService;
        _userService = userService;
        _timerScheduler = timerScheduler;

        StartCommand = new Command(async () => await ExecuteMotorActionAsync(MotorActionType.Start), () => CanExecute(MotorActionType.Start));
        StopCommand = new Command(async () => await ExecuteMotorActionAsync(MotorActionType.Stop), () => CanExecute(MotorActionType.Stop));
        StatusCommand = new Command(async () => await ExecuteMotorActionAsync(MotorActionType.Status), () => CanExecute(MotorActionType.Status));
        ScheduleCommand = new Command(async () => await ScheduleAsync(), () => SelectedUser is not null);
        AddUserCommand = new Command(async () => await AddUserAsync());
        RefreshCommand = new Command(async () => await LoadLogsAsync());

        _motorControllerService.ActionProcessed += OnActionProcessed;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _motorControllerService.InitializeAsync().ConfigureAwait(false);
            await _timerScheduler.InitializeAsync().ConfigureAwait(false);
            await LoadUsersAsync().ConfigureAwait(false);
            await LoadLogsAsync().ConfigureAwait(false);
            SelectedUser ??= Users.FirstOrDefault();
            _isInitialized = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteMotorActionAsync(MotorActionType actionType)
    {
        if (SelectedUser is null)
        {
            await ShowAlertAsync("Select User", "Please select a user before performing operations.");
            return;
        }

        try
        {
            IsBusy = true;
            MotorActionLog log = actionType switch
            {
                MotorActionType.Start => await _motorControllerService.StartMotorAsync(SelectedUser).ConfigureAwait(false),
                MotorActionType.Stop => await _motorControllerService.StopMotorAsync(SelectedUser).ConfigureAwait(false),
                _ => await _motorControllerService.RequestStatusAsync(SelectedUser).ConfigureAwait(false)
            };

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Logs.Any(l => l.Id == log.Id))
                {
                    Logs.Insert(0, log);
                }
                CurrentStatus = DescribeStatus(log);
            });
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Operation failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ScheduleAsync()
    {
        if (SelectedUser is null)
        {
            await ShowAlertAsync("Select User", "Please select a user to schedule.");
            return;
        }

        var executeAt = ScheduleDate.Date + ScheduleTime;
        if (executeAt <= DateTime.Now)
        {
            await ShowAlertAsync("Invalid time", "Choose a time in the future.");
            return;
        }

        try
        {
            IsBusy = true;
            var scheduled = new ScheduledMotorAction
            {
                ActionType = SelectedScheduleAction,
                ExecuteAt = executeAt,
                RequestedBy = SelectedUser.Name
            };

            var log = await _motorControllerService.ScheduleActionAsync(SelectedUser, scheduled).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() => Logs.Insert(0, log));
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Scheduling failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddUserAsync()
    {
        var name = await DisplayPromptAsync("Add User", "Enter the user's name:");
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var phone = await DisplayPromptAsync("Add User", "Enter the user's phone number:");
        if (string.IsNullOrWhiteSpace(phone))
        {
            return;
        }

        var role = await DisplayActionSheetAsync("Select Role", "Cancel", null, "Master", "Regular");
        if (string.Equals(role, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var user = new User
        {
            Name = name.Trim(),
            PhoneNumber = phone.Trim(),
            Role = string.Equals(role, "Master", StringComparison.OrdinalIgnoreCase) ? UserRole.Master : UserRole.Regular
        };

        if (user.Role == UserRole.Regular)
        {
            user.AllowedActions = new HashSet<MotorActionType>
            {
                MotorActionType.Start,
                MotorActionType.Stop,
                MotorActionType.Status
            };
        }

        try
        {
            IsBusy = true;
            user = await _userService.AddOrUpdateUserAsync(user).ConfigureAwait(false);
            await LoadUsersAsync().ConfigureAwait(false);
            SelectedUser = Users.FirstOrDefault(u => u.Id == user.Id);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync("Unable to add user", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadUsersAsync()
    {
        var users = await _userService.GetUsersAsync().ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
        });
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            IsBusy = true;
            var logs = await _motorLogService.GetLogsAsync().ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Logs.Clear();
                foreach (var log in logs.OrderByDescending(l => l.Timestamp))
                {
                    Logs.Add(log);
                }
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshCommandStates()
    {
        (StartCommand as Command)?.ChangeCanExecute();
        (StopCommand as Command)?.ChangeCanExecute();
        (StatusCommand as Command)?.ChangeCanExecute();
        (ScheduleCommand as Command)?.ChangeCanExecute();
    }

    private bool CanExecute(MotorActionType actionType)
    {
        if (SelectedUser is null)
        {
            return false;
        }

        return actionType switch
        {
            MotorActionType.Start => SelectedUser.CanStart,
            MotorActionType.Stop => SelectedUser.CanStop,
            MotorActionType.Status => SelectedUser.CanViewStatus,
            _ => false
        };
    }

    private void OnActionProcessed(object? sender, MotorActionLog log)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = Logs.FirstOrDefault(l => l.Id == log.Id);
            if (existing is not null)
            {
                existing.Response = log.Response;
                existing.Timestamp = log.Timestamp;
            }
            else
            {
                Logs.Insert(0, log);
            }

            var status = DescribeStatus(log);
            if (!string.IsNullOrWhiteSpace(status))
            {
                CurrentStatus = status;
            }
        });
    }

    private static string DescribeStatus(MotorActionLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.Response))
        {
            var interpreted = log.Response.ToLowerInvariant();
            if (interpreted.Contains("start") || interpreted.Contains("on"))
            {
                return "Motor Started";
            }

            if (interpreted.Contains("stop") || interpreted.Contains("off"))
            {
                return "Motor Stopped";
            }

            if (interpreted.Contains("error") || interpreted.Contains("fault"))
            {
                return "Error";
            }
        }

        return log.ActionType switch
        {
            MotorActionType.Start => "Motor Start Pending",
            MotorActionType.Stop => "Motor Stop Pending",
            MotorActionType.TimerStart => "Motor Start Scheduled",
            MotorActionType.TimerStop => "Motor Stop Scheduled",
            _ => string.Empty
        };
    }

    private static Task ShowAlertAsync(string title, string message)
    {
        if (Application.Current?.MainPage is null)
        {
            return Task.CompletedTask;
        }

        return Application.Current.MainPage.DisplayAlert(title, message, "OK");
    }

    private static Task<string?> DisplayPromptAsync(string title, string message)
    {
        if (Application.Current?.MainPage is null)
        {
            return Task.FromResult<string?>(null);
        }

        return Application.Current.MainPage.DisplayPromptAsync(title, message);
    }

    private static Task<string?> DisplayActionSheetAsync(string title, string cancel, string? destruction, params string[] buttons)
    {
        if (Application.Current?.MainPage is null)
        {
            return Task.FromResult<string?>(null);
        }

        return Application.Current.MainPage.DisplayActionSheet(title, cancel, destruction, buttons);
    }
}
