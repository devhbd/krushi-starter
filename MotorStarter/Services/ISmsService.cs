using MotorStarter.Models;

namespace MotorStarter.Services;

public record SmsMessageEventArgs(string Message);

public interface ISmsService
{
    const string MotorControllerNumber = "7249227760";

    event EventHandler<SmsMessageEventArgs>? MessageReceived;

    Task EnsurePermissionsAsync(CancellationToken cancellationToken = default);

    Task SendCommandAsync(string command, CancellationToken cancellationToken = default);
}
