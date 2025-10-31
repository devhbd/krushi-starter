# Motor Starter

Motor Starter is a .NET MAUI Android application that helps farmers and field technicians control their irrigation motor through SMS commands. The app offers a modern control panel with role-based access, scheduling, and persistent activity logs.

## Features

- **SMS Control:** Send predefined start, stop, and status SMS commands to the motor controller at `7249227760`.
- **Response Tracking:** Automatically captures controller replies to update the latest motor status and enrich the activity history.
- **User Management:** Maintain a master user and create restricted regular users with limited permissions.
- **Scheduling:** Set future start/stop actions using the in-app scheduler. Actions trigger automatically at the configured time.
- **Activity Log:** Persist history in a local SQLite database for offline reference.
- **Smart Alerts:** Surface failures and controller errors in the log feed so you can react quickly.

## Project Structure

- `MotorStarter/` – Main MAUI project.
  - `Views/` – XAML pages (currently a single dashboard page).
  - `ViewModels/` – MVVM view models that power the UI.
  - `Services/` – Platform-agnostic services for SMS, logging, users, and scheduling.
  - `Platforms/Android/` – Android-specific bootstrapping and SMS plumbing.
  - `Resources/` – Styling assets and the app icon.

## Getting Started

1. Install the [.NET 8 or newer](https://dotnet.microsoft.com/en-us/download) SDK with MAUI workloads.
2. From the repository root, restore and build the project:

   ```bash
   dotnet restore MotorStarter/MotorStarter.csproj
   dotnet build MotorStarter/MotorStarter.csproj -t:Run -f net10.0-android
   ```

3. Deploy to an Android device that supports SMS and grant the requested permissions when prompted.

> **Note:** The repository cannot send SMS messages during automated validation. All SMS actions must be exercised on a physical device or emulator configured with telephony support.

## Configuration

- Update the constant `MotorControllerNumber` in `Services/ISmsService.cs` if your controller uses a different phone number.
- The default SMS command mapping (`555` for start, `000` for stop, `888` for status) lives in `Services/MotorControllerService.cs`. Adjust these values to match your controller firmware.

## License

This project is provided as a starter template. Customize it to fit your deployment needs.
