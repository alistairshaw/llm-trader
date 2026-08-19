using Microsoft.Win32;

namespace BuildConventionsFixtures.PlatformCompatibility;

internal static class Violation
{
    internal static object? ReadWindowsRegistry()
    {
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\TradingPlatform", "Mode", null);
    }
}
