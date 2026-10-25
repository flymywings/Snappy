using Microsoft.Win32;

namespace Snappy.Windows;

internal static class LoginStartup
{
    internal static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)
            ?? throw new IOException("Could not access startup preferences.");
        if (enabled)
        {
            var executable = Environment.ProcessPath ?? throw new IOException("Cannot determine Snappy executable path.");
            key.SetValue("Snappy", "\"" + executable + "\" --background", RegistryValueKind.String);
        }
        else key.DeleteValue("Snappy", false);
    }
}
