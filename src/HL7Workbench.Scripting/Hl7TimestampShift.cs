using System.Globalization;

namespace HL7Workbench.Scripting;

/// <summary>Calendar-day shifts without adding precision, guessing a timezone or using the host clock.</summary>
internal static class Hl7TimestampShift
{
    internal static bool TryShift(string value, int days, out string shifted)
    {
        shifted = "";
        var suffix = ""; var body = value;
        var zone = value.LastIndexOfAny(['+', '-']);
        if (zone >= 0)
        {
            suffix = value[zone..]; body = value[..zone];
            if (suffix.Length != 5 || !Digits(suffix.AsSpan(1)) || !int.TryParse(suffix.AsSpan(1, 2), out var hours) ||
                !int.TryParse(suffix.AsSpan(3, 2), out var minutes) || minutes >= 60 || hours > 14 || hours == 14 && minutes != 0 ||
                suffix[0] == '-' && (hours > 12 || hours == 12 && minutes != 0)) return false;
        }
        var dot = body.IndexOf('.'); var fraction = "";
        if (dot >= 0)
        {
            fraction = body[dot..]; body = body[..dot];
            if (body.Length != 14 || fraction.Length is < 2 or > 5 || !Digits(fraction.AsSpan(1))) return false;
        }
        // Year/month-only values have no day to shift. Refuse rather than invent
        // precision or silently distort relative intervals at a calendar boundary.
        if (body.Length is not (8 or 10 or 12 or 14) || !Digits(body)) return false;
        var format = "yyyyMMddHHmmss"[..body.Length];
        if (!DateTime.TryParseExact(body, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        try { shifted = date.AddDays(days).ToString(format, CultureInfo.InvariantCulture) + fraction + suffix; return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
    private static bool Digits(ReadOnlySpan<char> value) => value.Length > 0 && value.IndexOfAnyExceptInRange('0', '9') < 0;
}
