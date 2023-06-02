using GeolocatorPlugin.Abstractions;
using Microsoft.Maui.Devices.Sensors;

namespace GeolocatorPlugin;

/// <summary>
/// Cross platform Geolocator implemenations
/// </summary>
public class CrossGeolocator
{
    static Lazy<IGeolocator> implementation = new Lazy<IGeolocator>(() => CreateGeolocator(), System.Threading.LazyThreadSafetyMode.PublicationOnly);
    /// <summary>
    /// Gets if the plugin is supported on the current platform.
    /// </summary>
    public static bool IsSupported => implementation.Value == null ? false : true;

    /// <summary>
    /// Current plugin implementation to use
    /// </summary>
    public static IGeolocator Current
    {
        get
        {
            var ret = implementation.Value;
            if (ret == null)
            {
                throw NotImplementedInReferenceAssembly();
            }
            return ret;
        }
    }

    static IGeolocator CreateGeolocator()
    {
#if ANDROID
            return new Platforms.Android.GeolocatorImplementation();
#elif IOS
            return new Platforms.iOS.GeolocatorImplementation();
#elif MACCATALYST
            return new Platforms.MacCatalyst.GeolocatorImplementation();
#elif WINDOWS
            return new Platforms.Windows.GeolocatorImplementation();
#endif

        throw new PlatformNotSupportedException();
    }

    internal static Exception NotImplementedInReferenceAssembly() =>
        new NotImplementedException("This functionality is not implemented in the portable version of this assembly.  You should reference the NuGet package from your main application project in order to reference the platform-specific implementation.");

}
