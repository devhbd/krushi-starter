using MotorStarter.Models;

namespace MotorStarter.Services;

public interface IUserService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<User> GetMasterUserAsync(CancellationToken cancellationToken = default);
    Task<User> AddOrUpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
}
