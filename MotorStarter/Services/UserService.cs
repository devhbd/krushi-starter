using System.Text.Json;
using Microsoft.Maui.Storage;
using MotorStarter.Models;

namespace MotorStarter.Services;

public class UserService : IUserService
{
    private const string FileName = "users.json";
    private readonly List<User> _users = new();
    private bool _initialized;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            var path = GetFilePath();
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = JsonSerializer.Deserialize<List<User>>(json);
                    if (data is not null)
                    {
                        _users.Clear();
                        _users.AddRange(data);
                    }
                }
            }

            if (_users.All(u => u.Role != UserRole.Master))
            {
                _users.Add(new User
                {
                    Id = 1,
                    Name = "Master",
                    PhoneNumber = ISmsService.MotorControllerNumber,
                    Role = UserRole.Master,
                    AllowedActions = new HashSet<MotorActionType>
                    {
                        MotorActionType.Start,
                        MotorActionType.Stop,
                        MotorActionType.Status,
                        MotorActionType.TimerStart,
                        MotorActionType.TimerStop
                    }
                });
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<User> GetMasterUserAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var master = _users.First(u => u.Role == UserRole.Master);
        return Clone(master);
    }

    public async Task<User> AddOrUpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = _users.FirstOrDefault(u => u.Id == user.Id || (!string.IsNullOrWhiteSpace(user.PhoneNumber) && u.PhoneNumber == user.PhoneNumber));
            if (existing is null)
            {
                user.Id = _users.Count == 0 ? 1 : _users.Max(u => u.Id) + 1;
                _users.Add(Clone(user));
            }
            else
            {
                existing.Name = user.Name;
                existing.PhoneNumber = user.PhoneNumber;
                existing.Role = user.Role;
                existing.AllowedActions = new HashSet<MotorActionType>(user.AllowedActions);
            }

            await PersistAsync(cancellationToken).ConfigureAwait(false);
            return Clone(user);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        return _users.Select(Clone).ToList();
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var path = GetFilePath();
        var json = JsonSerializer.Serialize(_users);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    private static string GetFilePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, FileName);
    }

    private static User Clone(User user)
    {
        return new User
        {
            Id = user.Id,
            Name = user.Name,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            AllowedActions = new HashSet<MotorActionType>(user.AllowedActions)
        };
    }
}
