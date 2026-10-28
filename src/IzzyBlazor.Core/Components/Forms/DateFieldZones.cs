using System.Globalization;

namespace IzzyBlazor;

/// <summary>How a <see cref="DateField{TValue}"/> names its time zone to the script.</summary>
internal static class DateFieldZones
{
    /// <summary>
    /// A zone the browser's <c>Intl</c> knows: its IANA id (Windows ids converted), or, for a zone
    /// with a fixed offset or no IANA id, that offset (<c>+05:30</c>).
    /// </summary>
    public static string ScriptId(TimeZoneInfo zone)
    {
        if (zone.SupportsDaylightSavingTime)
        {
            if (zone.HasIanaId) return zone.Id;
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana)) return iana;
        }

        var offset = zone.BaseUtcOffset;
        return (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }
}
