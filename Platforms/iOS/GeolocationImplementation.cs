﻿using CoreLocation;
using Foundation;
using GeolocatorPlugin.Abstractions;
using UIKit;

namespace GeolocatorPlugin.Platforms.iOS;

/// <summary>
/// Implementation for Geolocator
/// </summary>
[Preserve(AllMembers = true)]
public class GeolocatorImplementation : IGeolocator
{
    bool deferringUpdates;
    readonly CLLocationManager manager;
    bool includeHeading;
    bool isListening;
    Position lastPosition;
    ListenerSettings listenerSettings;

    /// <summary>
    /// Constructor for implementation
    /// </summary>
    public GeolocatorImplementation()
    {
        DesiredAccuracy = 100;
        manager = GetManager();
        manager.AuthorizationChanged += OnAuthorizationChanged;
        manager.Failed += OnFailed;


        if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
            manager.LocationsUpdated += OnLocationsUpdated;
        else
            manager.UpdatedLocation += OnUpdatedLocation;


        manager.DeferredUpdatesFinished += OnDeferredUpdatedFinished;

    }

    void OnDeferredUpdatedFinished(object sender, NSErrorEventArgs e) => deferringUpdates = false;


    bool CanDeferLocationUpdate => CLLocationManager.DeferredLocationUpdatesAvailable && UIDevice.CurrentDevice.CheckSystemVersion(6, 0);


    async Task<bool> CheckWhenInUsePermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        {
            Console.WriteLine("Currently does not have Location permissions, requesting permissions");

            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                Console.WriteLine("Location permission denied, can not get positions async.");
                return false;
            }
        }

        return true;

    }
    async Task<bool> CheckAlwaysPermissions()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (status != PermissionStatus.Granted)
        {
            Console.WriteLine("Currently does not have Location permissions, requesting permissions");

            status = await Permissions.RequestAsync<Permissions.LocationAlways>();

            if (status != PermissionStatus.Granted)
            {
                Console.WriteLine("Location permission denied, can not get positions async.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Position error event handler
    /// </summary>
    public event EventHandler<PositionErrorEventArgs> PositionError;

    /// <summary>
    /// Position changed event handler
    /// </summary>
    public event EventHandler<PositionEventArgs> PositionChanged;

    /// <summary>
    /// Desired accuracy in meters
    /// </summary>
    public double DesiredAccuracy
    {
        get;
        set;
    }

    /// <summary>
    /// Gets if you are listening for location changes
    ///
    public bool IsListening => isListening;

    /// <summary>
    /// Gets if device supports heading (course)
    /// </summary>
    public bool SupportsHeading => true;

    /// <summary>
    /// Gets if geolocation is available on device
    /// </summary>
    public bool IsGeolocationAvailable => true; //all iOS devices support Geolocation

    /// <summary>
    /// Gets if geolocation is enabled on device
    /// </summary>
    public bool IsGeolocationEnabled
    {
        get
        {
            var status = CLLocationManager.Status;
            return CLLocationManager.LocationServicesEnabled;
        }
    }

    void RequestAuthorization()
    {
        var info = NSBundle.MainBundle.InfoDictionary;

        if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0))
        {
            if (info.ContainsKey(new NSString("NSLocationAlwaysUsageDescription")))
                manager.RequestAlwaysAuthorization();
            else if (info.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription")))
                manager.RequestWhenInUseAuthorization();
            else
                throw new UnauthorizedAccessException("On iOS 8.0 and higher you must set either NSLocationWhenInUseUsageDescription or NSLocationAlwaysUsageDescription in your Info.plist file to enable Authorization Requests for Location updates!");
        }
    }

    /// <summary>
    /// Gets the last known and most accurate location.
    /// This is usually cached and best to display first before querying for full position.
    /// </summary>
    /// <returns>Best and most recent location or null if none found</returns>
    public async Task<Position> GetLastKnownLocationAsync()
    {
        var hasPermission = await CheckWhenInUsePermission();
        if (!hasPermission)
            throw new GeolocationException(GeolocationError.Unauthorized);

        var m = GetManager();
        var newLocation = m?.Location;

        if (newLocation == null)
            return null;

        var position = new Position();
        position.HasAccuracy = true;
        position.Accuracy = newLocation.HorizontalAccuracy;
        position.HasAltitude = newLocation.Altitude > -1;
        position.Altitude = newLocation.Altitude;
        position.AltitudeAccuracy = newLocation.VerticalAccuracy;
        position.HasLatitudeLongitude = newLocation.HorizontalAccuracy > -1;
        position.Latitude = newLocation.Coordinate.Latitude;
        position.Longitude = newLocation.Coordinate.Longitude;

        try
        {
            position.Timestamp = new DateTimeOffset(newLocation.Timestamp.ToDateTime());
        }
        catch (Exception)
        {
            position.Timestamp = DateTimeOffset.UtcNow;
        }

        return position;
    }

    /// <summary>
    /// Gets position async with specified parameters
    /// </summary>
    /// <param name="timeout">Timeout to wait, Default Infinite</param>
    /// <param name="cancelToken">Cancelation token</param>
    /// <param name="includeHeading">If you would like to include heading</param>
    /// <returns>Position</returns>
    public async Task<Position> GetPositionAsync(TimeSpan? timeout, CancellationToken? cancelToken = null, bool includeHeading = false)
    {
        var hasPermission = await CheckWhenInUsePermission();
        if (!hasPermission)
            throw new GeolocationException(GeolocationError.Unauthorized);

        var timeoutMilliseconds = timeout.HasValue ? (int)timeout.Value.TotalMilliseconds : Timeout.Infinite;

        if (timeoutMilliseconds <= 0 && timeoutMilliseconds != Timeout.Infinite)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive or Timeout.Infinite");

        if (!cancelToken.HasValue)
            cancelToken = CancellationToken.None;

        TaskCompletionSource<Position> tcs;
        if (!IsListening)
        {
            var m = GetManager();
            m.DesiredAccuracy = DesiredAccuracy;

            tcs = new TaskCompletionSource<Position>(m);
            var singleListener = new GeolocationSingleUpdateDelegate(m, DesiredAccuracy, includeHeading, timeoutMilliseconds, cancelToken.Value);
            m.Delegate = singleListener;

            m.StartUpdatingLocation();

            return await singleListener.Task;
        }


        tcs = new TaskCompletionSource<Position>();
        if (lastPosition == null)
        {
            if (cancelToken != CancellationToken.None)
            {
                cancelToken.Value.Register(() => tcs.TrySetCanceled());
            }

            EventHandler<PositionErrorEventArgs> gotError = null;
            gotError = (s, e) =>
            {
                tcs.TrySetException(new GeolocationException(e.Error));
                PositionError -= gotError;
            };

            PositionError += gotError;

            EventHandler<PositionEventArgs> gotPosition = null;
            gotPosition = (s, e) =>
            {
                tcs.TrySetResult(e.Position);
                PositionChanged -= gotPosition;
            };

            PositionChanged += gotPosition;
        }
        else
            tcs.SetResult(lastPosition);


        return await tcs.Task;
    }

    /// <summary>
    /// Retrieve positions for address.
    /// </summary>
    /// <param name="address">Desired address</param>
    /// <param name="mapKey">Map Key required only on UWP</param>
    /// <returns>Positions of the desired address</returns>
    public async Task<IEnumerable<Position>> GetPositionsForAddressAsync(string address, string mapKey = null)
    {
        if (address == null)
            throw new ArgumentNullException(nameof(address));

        using (var geocoder = new CLGeocoder())
        {
            var positionList = await geocoder.GeocodeAddressAsync(address);
            return positionList.Select(p => new Position
            {
                HasLatitudeLongitude = true,
                Latitude = p.Location.Coordinate.Latitude,
                Longitude = p.Location.Coordinate.Longitude
            });
        }
    }

    /// <summary>
    /// Retrieve addresses for position.
    /// </summary>
    /// <param name="position">Desired position (latitude and longitude)</param>
    /// <returns>Addresses of the desired position</returns>
    public async Task<IEnumerable<Address>> GetAddressesForPositionAsync(Position position, string mapKey = null)
    {
        if (position == null)
            throw new ArgumentNullException(nameof(position));

        using (var geocoder = new CLGeocoder())
        {
            var addressList = await geocoder.ReverseGeocodeLocationAsync(new CLLocation(position.Latitude, position.Longitude));

            return addressList?.ToAddresses() ?? null;
        }
    }

    /// <summary>
    /// Start listening for changes
    /// </summary>
    /// <param name="minimumTime">Time</param>
    /// <param name="minimumDistance">Distance</param>
    /// <param name="includeHeading">Include heading or not</param>
    /// <param name="listenerSettings">Optional settings (iOS only)</param>
    public async Task<bool> StartListeningAsync(TimeSpan minimumTime, double minimumDistance, bool includeHeading = false, ListenerSettings listenerSettings = null)
    {
        if (minimumDistance < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDistance));

        if (isListening)
            throw new InvalidOperationException("Already listening");

        // if no settings were passed in, instantiate the default settings. need to check this and create default settings since
        // previous calls to StartListeningAsync might have already configured the location manager in a non-default way that the
        // caller of this method might not be expecting. the caller should expect the defaults if they pass no settings.
        if (listenerSettings == null)
            listenerSettings = new ListenerSettings();

        var hasPermission = false;
        if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0))
        {
            if (listenerSettings.RequireLocationAlwaysPermission)
            {
                hasPermission = await CheckAlwaysPermissions();
            }
            else
            {
                hasPermission = await CheckWhenInUsePermission();
            }
        }

        if (!hasPermission)
            throw new GeolocationException(GeolocationError.Unauthorized);

        this.includeHeading = includeHeading;

        // keep reference to settings so that we can stop the listener appropriately later
        this.listenerSettings = listenerSettings;

        var desiredAccuracy = DesiredAccuracy;

        // set background flag
        if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            manager.ShowsBackgroundLocationIndicator = listenerSettings.ShowsBackgroundLocationIndicator;

        if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0))
            manager.AllowsBackgroundLocationUpdates = listenerSettings.AllowBackgroundUpdates;

        // configure location update pausing
        if (UIDevice.CurrentDevice.CheckSystemVersion(6, 0))
        {
            manager.PausesLocationUpdatesAutomatically = listenerSettings.PauseLocationUpdatesAutomatically;

            switch (listenerSettings.ActivityType)
            {
                case ActivityType.AutomotiveNavigation:
                    manager.ActivityType = CLActivityType.AutomotiveNavigation;
                    break;
                case ActivityType.Fitness:
                    manager.ActivityType = CLActivityType.Fitness;
                    break;
                case ActivityType.OtherNavigation:
                    manager.ActivityType = CLActivityType.OtherNavigation;
                    break;
                default:
                    manager.ActivityType = CLActivityType.Other;
                    break;
            }
        }

        // to use deferral, CLLocationManager.DistanceFilter must be set to CLLocationDistance.None, and CLLocationManager.DesiredAccuracy must be 
        // either CLLocation.AccuracyBest or CLLocation.AccuracyBestForNavigation. deferral only available on iOS 6.0 and above.
        if (CanDeferLocationUpdate && listenerSettings.DeferLocationUpdates)
        {
            minimumDistance = CLLocationDistance.FilterNone;
            desiredAccuracy = CLLocation.AccuracyBest;
        }

        isListening = true;
        manager.DesiredAccuracy = desiredAccuracy;
        manager.DistanceFilter = minimumDistance;

        if (listenerSettings.ListenForSignificantChanges)
            manager.StartMonitoringSignificantLocationChanges();
        else
            manager.StartUpdatingLocation();

        return true;
    }

    /// <summary>
    /// Stop listening
    /// </summary>
    public Task<bool> StopListeningAsync()
    {
        if (!isListening)
            return Task.FromResult(true);

        isListening = false;
        // it looks like deferred location updates can apply to the standard service or significant change service. disallow deferral in either case.
        if ((listenerSettings?.DeferLocationUpdates ?? false) && CanDeferLocationUpdate)
            manager.DisallowDeferredLocationUpdates();


        if (listenerSettings?.ListenForSignificantChanges ?? false)
            manager.StopMonitoringSignificantLocationChanges();
        else
            manager.StopUpdatingLocation();

        listenerSettings = null;
        lastPosition = null;

        return Task.FromResult(true);
    }

    CLLocationManager GetManager()
    {
        CLLocationManager m = null;
        new NSObject().InvokeOnMainThread(() => m = new CLLocationManager());
        return m;
    }

    void OnLocationsUpdated(object sender, CLLocationsUpdatedEventArgs e)
    {
        if (e.Locations.Any())
        {
            UpdatePosition(e.Locations.Last());
        }

        // defer future location updates if requested
        if ((listenerSettings?.DeferLocationUpdates ?? false) && !deferringUpdates && CanDeferLocationUpdate)
        {
#if __IOS__
            manager.AllowDeferredLocationUpdatesUntil(listenerSettings.DeferralDistanceMeters == null ? CLLocationDistance.MaxDistance : listenerSettings.DeferralDistanceMeters.GetValueOrDefault(),
                listenerSettings.DeferralTime == null ? CLLocationManager.MaxTimeInterval : listenerSettings.DeferralTime.GetValueOrDefault().TotalSeconds);
#endif

            deferringUpdates = true;
        }
    }

    void OnUpdatedLocation(object sender, CLLocationUpdatedEventArgs e) => UpdatePosition(e.NewLocation);


    void UpdatePosition(CLLocation location)
    {
        var p = (lastPosition == null) ? new Position() : new Position(this.lastPosition);
        p.HasAccuracy = true;

        if (location.HorizontalAccuracy > -1)
        {
            p.Accuracy = location.HorizontalAccuracy;
            p.HasLatitudeLongitude = true;
            p.Latitude = location.Coordinate.Latitude;
            p.Longitude = location.Coordinate.Longitude;
        }

        if (location.VerticalAccuracy > -1)
        {
            p.HasAltitude = true;
            p.Altitude = location.Altitude;
            p.AltitudeAccuracy = location.VerticalAccuracy;
        }

        if (location.Speed > -1)
        {
            p.HasSpeed = true;
            p.Speed = location.Speed;
        }


        if (includeHeading && location.Course > -1)
        {
            p.HasHeading = true;
            p.Heading = location.Course;
        }

        try
        {
            var date = location.Timestamp.ToDateTime();
            p.Timestamp = new DateTimeOffset(date);
        }
        catch (Exception)
        {
            p.Timestamp = DateTimeOffset.UtcNow;
        }


        lastPosition = p;

        OnPositionChanged(new PositionEventArgs(p));

        location.Dispose();
    }




    void OnPositionChanged(PositionEventArgs e) => PositionChanged?.Invoke(this, e);


    async void OnPositionError(PositionErrorEventArgs e)
    {
        await StopListeningAsync();
        PositionError?.Invoke(this, e);
    }

    void OnFailed(object sender, NSErrorEventArgs e)
    {
        if ((CLError)(int)e.Error.Code == CLError.Network)
            OnPositionError(new PositionErrorEventArgs(GeolocationError.PositionUnavailable));
    }

    void OnAuthorizationChanged(object sender, CLAuthorizationChangedEventArgs e)
    {
        if (e.Status == CLAuthorizationStatus.Denied || e.Status == CLAuthorizationStatus.Restricted)
            OnPositionError(new PositionErrorEventArgs(GeolocationError.Unauthorized));
    }


}