using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace GeolocatorPlugin.Platforms.Windows;

internal static class Extensions
{
    public static ConfiguredTaskAwaitable<T> AsTask<T>(this IAsyncOperation<T> self, bool continueOnCapturedContext)
    {
        return self.AsTask().ConfigureAwait(continueOnCapturedContext);
    }
}
