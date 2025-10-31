using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Telephony;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;

namespace MotorStarter.Services;

public partial class SmsService
{
    private SmsBroadcastReceiver? _receiver;

    partial void PlatformSendSms(string message)
    {
        var smsManager = SmsManager.Default;
        smsManager?.SendTextMessage(ISmsService.MotorControllerNumber, null, message, null, null);
    }

    partial void PlatformInitializeReceiver()
    {
        if (_receiver is not null)
        {
            return;
        }

        _receiver = new SmsBroadcastReceiver(this);
        var filter = new IntentFilter("android.provider.Telephony.SMS_RECEIVED")
        {
            Priority = (int)IntentFilterPriority.HighPriority
        };

        Application.Context.RegisterReceiver(_receiver, filter);
    }

    async partial Task PlatformEnsurePermissionsAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        if (ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.SendSms) != Permission.Granted)
        {
            await Permissions.RequestAsync<Permissions.Sms>().WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.ReadSms) != Permission.Granted)
        {
            RequestPermission(Manifest.Permission.ReadSms);
        }

        if (ContextCompat.CheckSelfPermission(Application.Context, Manifest.Permission.ReceiveSms) != Permission.Granted)
        {
            RequestPermission(Manifest.Permission.ReceiveSms);
        }
    }

    private static void RequestPermission(string permission)
    {
        var activity = Platform.CurrentActivity;
        if (activity is null)
        {
            return;
        }

        ActivityCompat.RequestPermissions(activity, new[] { permission }, 1010);
    }

    private sealed class SmsBroadcastReceiver : BroadcastReceiver
    {
        private readonly WeakReference<SmsService> _service;

        public SmsBroadcastReceiver(SmsService service)
        {
            _service = new WeakReference<SmsService>(service);
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent is null || intent.Action != "android.provider.Telephony.SMS_RECEIVED")
            {
                return;
            }

            var bundle = intent.Extras;
            if (bundle is null)
            {
                return;
            }

            var pdus = bundle.Get("pdus") as Java.Lang.Object[];
            if (pdus is null)
            {
                return;
            }

            var messages = new List<string>();
            foreach (var pdu in pdus)
            {
                var format = bundle.GetString("format");
                var sms = Build.VERSION.SdkInt >= BuildVersionCodes.M
                    ? SmsMessage.CreateFromPdu((byte[])pdu, format)
                    : SmsMessage.CreateFromPdu((byte[])pdu);
                if (sms?.OriginatingAddress?.Contains(ISmsService.MotorControllerNumber, StringComparison.OrdinalIgnoreCase) == true)
                {
                    messages.Add(sms.MessageBody ?? string.Empty);
                }
            }

            if (messages.Count == 0)
            {
                return;
            }

            var combined = string.Join('\n', messages);
            if (_service.TryGetTarget(out var service))
            {
                service.RaiseMessageReceived(combined.Trim());
            }
        }
    }
}
