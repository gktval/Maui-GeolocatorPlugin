using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeolocatorPlugin.Abstractions;

/// <summary>
/// Geolocator Plugin Utililities
/// </summary>
public static class GeolocatorUtils
{
    /// <summary>
    /// Calculates the distance in miles
    /// </summary>
    /// <returns>The distance.</returns>
    /// <param name="latitudeStart">Latitude start.</param>
    /// <param name="longitudeStart">Longitude start.</param>
    /// <param name="latitudeEnd">Latitude end.</param>
    /// <param name="longitudeEnd">Longitude end.</param>
    /// <param name="units">Units to return</param>
    public static double CalculateDistance(double latitudeStart, double longitudeStart,
        double latitudeEnd, double longitudeEnd, DistanceUnits units = DistanceUnits.Miles)
    {
        if (latitudeEnd == latitudeStart && longitudeEnd == longitudeStart)
            return 0;

        var rlat1 = Math.PI * latitudeStart / 180.0;
        var rlat2 = Math.PI * latitudeEnd / 180.0;
        var theta = longitudeStart - longitudeEnd;
        var rtheta = Math.PI * theta / 180.0;
        var dist = Math.Sin(rlat1) * Math.Sin(rlat2) + Math.Cos(rlat1) * Math.Cos(rlat2) * Math.Cos(rtheta);
        dist = Math.Acos(dist);
        dist = dist * 180.0 / Math.PI;
        var final = dist * 60.0 * 1.1515;
        if (double.IsNaN(final) || double.IsInfinity(final) || double.IsNegativeInfinity(final) ||
            double.IsPositiveInfinity(final) || final < 0)
            return 0;

        if (units == DistanceUnits.Kilometers)
            return MilesToKilometers(final);

        return final;
    }


    /// <summary>
    /// Calculates the distance in miles
    /// </summary>
    /// <returns>The distance.</returns>
    /// <param name="positionStart">Start position</param>
    /// <param name="positionEnd">End Position.</param>
    /// <param name="units">Units to return</param>
    public static double CalculateDistance(this Position positionStart, Position positionEnd, DistanceUnits units = DistanceUnits.Miles) =>
        CalculateDistance(positionStart.Latitude, positionStart.Longitude, positionEnd.Latitude, positionEnd.Longitude, units);



    /// <summary>
    /// Convert Miles to Kilometers
    /// </summary>
    /// <param name="miles"></param>
    /// <returns></returns>
    public static double MilesToKilometers(double miles) => miles * 1.609344;

    /// <summary>
    /// Convert Kilometers to Miles
    /// </summary>
    /// <param name="kilometers"></param>
    /// <returns></returns>
    public static double KilometersToMiles(double kilometers) => kilometers * .62137119;

    /// <summary>
    /// Units for the distance
    /// </summary>
    public enum DistanceUnits
    {
        /// <summary>
        /// Kilometers
        /// </summary>
        Kilometers,
        /// <summary>
        /// Miles
        /// </summary>
        Miles
    }
}
