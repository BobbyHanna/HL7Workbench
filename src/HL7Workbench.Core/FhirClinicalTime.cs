using System.Globalization;
using System.Text.RegularExpressions;

namespace HL7Workbench.Core;

public enum FhirTimePrecision { Missing, Invalid, Year, Month, Day, Instant }

/// <summary>Retains actual precision/text. Coarse ordering coordinates are never used as fabricated clinical times.</summary>
public sealed record FhirClinicalTime(string Text, FhirTimePrecision Precision, string Hl7, DateTimeOffset? Instant, long Order)
{
    public bool IsValid => Precision is not (FhirTimePrecision.Missing or FhirTimePrecision.Invalid);
    public bool IsInstant => Precision == FhirTimePrecision.Instant;
    public static FhirClinicalTime Parse(string? value)
    {
        var text = value ?? "";
        if (text.Length == 0) return new("", FhirTimePrecision.Missing, "", null, long.MaxValue);
        var match = Regex.Match(text, @"^(?<year>[0-9]{4})(?:-(?<month>[0-9]{2})(?:-(?<day>[0-9]{2}))?)?$", RegexOptions.CultureInvariant);
        if (match.Success)
        {
            var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            var month = match.Groups["month"].Success ? int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture) : 1;
            var day = match.Groups["day"].Success ? int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture) : 1;
            if (year is >= 1 and <= 9999 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
            {
                var precision = match.Groups["day"].Success ? FhirTimePrecision.Day : match.Groups["month"].Success ? FhirTimePrecision.Month : FhirTimePrecision.Year;
                return new(text, precision, text.Replace("-", "", StringComparison.Ordinal), null, new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).Ticks);
            }
        }
        if (text.Length <= 40 && Regex.IsMatch(text, @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,4})?(?:Z|[+-][0-9]{2}:[0-9]{2})$", RegexOptions.CultureInvariant) &&
            DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            // v2.5.1 TS supports up to four fractional digits; do not silently round higher precision.
            var fraction = text.IndexOf('.', StringComparison.Ordinal);
            var zone = text.EndsWith('Z') ? text.Length - 1 : text.Length - 6;
            var suffix = fraction < 0 ? "" : text[fraction..zone];
            var offset = text.EndsWith('Z') ? "+0000" : text[zone..].Replace(":", "", StringComparison.Ordinal);
            return new(text, FhirTimePrecision.Instant, instant.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + suffix + offset, instant, instant.UtcTicks);
        }
        return new(text, FhirTimePrecision.Invalid, "", null, long.MaxValue);
    }
    public override string ToString() => "FHIR clinical timestamp";
}
