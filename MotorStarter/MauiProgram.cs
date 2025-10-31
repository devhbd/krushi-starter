using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MotorStarter.Services;
using MotorStarter.ViewModels;
using MotorStarter.Views;

namespace MotorStarter;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<MainViewModel>();

        builder.Services.AddSingleton<ISmsService, SmsService>();
        builder.Services.AddSingleton<IMotorControllerService, MotorControllerService>();
        builder.Services.AddSingleton<IMotorLogService, SqliteMotorLogService>();
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton<ITimerScheduler, TimerScheduler>();

        return builder.Build();
    }
}
