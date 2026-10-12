namespace ProjectCopilot.Application.Common;

/// <summary>
/// Ensures a date/time value is expressed as a <see cref="DateTimeKind.Utc"/> instant, as
/// required by PostgreSQL "timestamp with time zone" columns. System.Text.Json deserializes a
/// date-only or timezone-less ISO string (e.g. "2026-11-07", as sent by an HTML
/// &lt;input type="date"&gt;) with Kind = Unspecified, which Npgsql rejects when writing to a
/// timestamptz column; an explicit-offset string (e.g. "...+00:00") deserializes with
/// Kind = Local instead.
///
/// Validators must normalize with this helper before comparing against "today" — comparing the
/// raw deserialized value's <c>.Date</c> implicitly reinterprets a Kind = Local instant relative
/// to the host's local timezone, which can let a genuinely past UTC instant slip past a
/// "no past dates" rule.
/// </summary>
public static class DateTimeNormalization
{
    public static DateTime? NormalizeToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
