namespace MotorStarter.Services;

public partial class SmsService
{
    partial void PlatformSendSms(string message)
    {
        System.Diagnostics.Debug.WriteLine($"SMS command skipped on unsupported platform: {message}");
    }

    partial void PlatformInitializeReceiver()
    {
    }

    partial Task PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
