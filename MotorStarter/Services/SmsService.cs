namespace MotorStarter.Services;

public partial class SmsService : ISmsService
{
    private bool _isInitialized;

    public event EventHandler<SmsMessageEventArgs>? MessageReceived;

    public async Task EnsurePermissionsAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await PlatformEnsurePermissionsAsync(cancellationToken).ConfigureAwait(false);
        PlatformInitializeReceiver();
        _isInitialized = true;
    }

    public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        PlatformSendSms(command);
        return Task.CompletedTask;
    }

    internal void RaiseMessageReceived(string message)
    {
        MessageReceived?.Invoke(this, new SmsMessageEventArgs(message));
    }

    partial void PlatformSendSms(string message);
    partial void PlatformInitializeReceiver();
    partial Task PlatformEnsurePermissionsAsync(CancellationToken cancellationToken);
}
