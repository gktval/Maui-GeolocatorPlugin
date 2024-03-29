﻿using GeolocatorPlugin.Abstractions;
using Windows.Services.Maps;

namespace GeolocatorPlugin.Platforms.Windows;

internal static class GeolocatorUtils
{
    internal static IEnumerable<Address> ToAddresses(this IEnumerable<MapLocation> addresses)
    {
        return addresses.Select(address => new Address
        {
            Longitude = address.Point.Position.Longitude,
            Latitude = address.Point.Position.Latitude,
            FeatureName = address.DisplayName,
            PostalCode = address.Address.PostCode,
            CountryCode = address.Address.CountryCode,
            CountryName = address.Address.Country,
            Thoroughfare = address.Address.Street,
            SubThoroughfare = address.Address.StreetNumber,
            Locality = address.Address.Town,
            AdminArea = address.Address.Region,
            SubAdminArea = address.Address.District
        });
    }
}
