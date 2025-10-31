using Microsoft.Maui.Storage;
using MotorStarter.Models;
using SQLite;

namespace MotorStarter.Services;

public class SqliteMotorLogService : IMotorLogService
{
    private const string DatabaseName = "motorstarter.db3";
    private SQLiteAsyncConnection? _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, DatabaseName);
                _connection = new SQLiteAsyncConnection(dbPath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
                await _connection.CreateTableAsync<MotorActionLogEntity>().ConfigureAwait(false);
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<IReadOnlyList<MotorActionLog>> GetLogsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var entities = await _connection!
            .Table<MotorActionLogEntity>()
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .ToListAsync()
            .ConfigureAwait(false);

        return entities
            .Select(ToDomain)
            .ToList();
    }

    public async Task SaveLogAsync(MotorActionLog log, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var entity = MotorActionLogEntity.FromDomain(log);
        await _connection!.InsertAsync(entity).ConfigureAwait(false);
        log.Id = entity.Id;
    }

    public async Task UpdateLogAsync(MotorActionLog log, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var entity = MotorActionLogEntity.FromDomain(log);
        await _connection!.UpdateAsync(entity).ConfigureAwait(false);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static MotorActionLog ToDomain(MotorActionLogEntity entity) => new()
    {
        Id = entity.Id,
        ActionType = entity.ActionType,
        Message = entity.Message,
        Response = entity.Response,
        Timestamp = entity.Timestamp,
        UserName = entity.UserName
    };

    private class MotorActionLogEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        public MotorActionType ActionType { get; set; }

        public string Message { get; set; } = string.Empty;

        public string Response { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public static MotorActionLogEntity FromDomain(MotorActionLog log) => new()
        {
            Id = log.Id,
            ActionType = log.ActionType,
            Message = log.Message,
            Response = log.Response,
            Timestamp = log.Timestamp,
            UserName = log.UserName
        };
    }
}
