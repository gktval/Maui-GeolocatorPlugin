using GeolocatorPlugin;
using GeolocatorPlugin.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Sample.ViewModels;

public class GPSViewModel : INotifyPropertyChanged
{
    private const double MIN_MARK_INTERVAL = 1.0d;

    public event PropertyChangedEventHandler PropertyChanged;

    private string _gpsData;
    private string _gpsButtonText;
    public string GPSData
    {
        get { return _gpsData; }
        set { _gpsData = value; OnPropertyChanged(nameof(GPSData)); }
    }
    public bool IsRunning { get; set; }
    public string GPSButtonText
    {
        get { return _gpsButtonText; }
        set { _gpsButtonText = value; OnPropertyChanged(nameof(GPSButtonText)); }
    }
    public Command ToggleGPSCommand { get; set; }

    public GPSViewModel()
    {
        ToggleGPSCommand = new Command(OnToggleGPS);
        GPSButtonText = "GPS Off";
    }

    public virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void OnToggleGPS(object obj)
    {
        await Toggle(!IsRunning);

        GPSButtonText = IsRunning ? "GPS On" : "GPS Off";
    }

    /// <summary>
    /// Activates/Deactives the gps device
    /// </summary>
    /// <param name="isOn"></param>
    public async Task<bool> Toggle(bool isOn)
    {
        BasePermission gpsPermission = new LocationWhenInUse();
        var hasPermission = await Utils.CheckPermissions(gpsPermission, true);
        if (!hasPermission)
            return false;

        if (!isOn)
        {
            if (await CrossGeolocator.Current.StopListeningAsync())
            {
                CrossGeolocator.Current.PositionChanged -= CrossGeolocator_Current_PositionChanged;
                CrossGeolocator.Current.PositionError -= CrossGeolocator_Current_PositionError;
            }
            GPSData = string.Empty;
            IsRunning = false;
        }
        else
        {
            float minTime = .5f;
            if (await CrossGeolocator.Current.StartListeningAsync(TimeSpan.FromSeconds(minTime), MIN_MARK_INTERVAL, true, new ListenerSettings
            {
                ActivityType = ActivityType.AutomotiveNavigation,
                AllowBackgroundUpdates = true,
                DeferLocationUpdates = false,
                ListenForSignificantChanges = false,
                PauseLocationUpdatesAutomatically = false,
                ShowsBackgroundLocationIndicator = true,
            }))
            {
                CrossGeolocator.Current.PositionChanged += CrossGeolocator_Current_PositionChanged;
                CrossGeolocator.Current.PositionError += CrossGeolocator_Current_PositionError;
            }

            IsRunning = true;
        }

        return true;
    }


    /// <summary>
    /// Handles Position Changed events from the plugin
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossGeolocator_Current_PositionChanged(object sender, PositionEventArgs e)
    {
        GPSData = string.Format("Time: {0} \nLat: {1} \nLong: {2} \nAltitude: {3} \nAltitude Accuracy: {4} \nAccuracy: {5} \nHeading: {6} \nSpeed: {7}",
            e.Position.Timestamp,
            e.Position.Latitude,
            e.Position.Longitude,
            e.Position.Altitude,
            e.Position.AltitudeAccuracy,
            e.Position.Accuracy,
            e.Position.Heading,
            e.Position.Speed);

        IsRunning = true;
    }

    /// <summary>
    /// Handles position errors
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossGeolocator_Current_PositionError(object sender, PositionErrorEventArgs e)
    {
        Console.WriteLine(e.Error.ToString());
    }

    /// <summary>
    /// Returns the Last Known Location of the device
    /// </summary>
    /// <returns></returns>
    internal async Task<Position> GetLastKnownLocation()
    {
        BasePermission gpsPermission = new LocationWhenInUse();
        var hasPermission = await Utils.CheckPermissions(gpsPermission, true);
        if (hasPermission)
        {
            var position = await CrossGeolocator.Current.GetLastKnownLocationAsync();
            CrossGeolocator_Current_PositionChanged(this, new PositionEventArgs(position));
            return position;
        }
        return null;
    }


}
