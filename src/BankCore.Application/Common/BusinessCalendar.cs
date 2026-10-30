namespace BankCore.Application.Common;

/// <summary>
/// The bank's business day follows Sri Lanka time (UTC+05:30, no daylight saving).
/// Everything is stored in UTC; this class converts business dates to UTC ranges.
/// </summary>
public static class BusinessCalendar
{
    public static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow + SriLankaOffset);

    public static DateTime StartOfBusinessDayUtc(DateTime utcNow) => ToUtcStart(Today(utcNow));

    public static DateTime ToUtcStart(DateOnly businessDate) =>
        DateTime.SpecifyKind(businessDate.ToDateTime(TimeOnly.MinValue) - SriLankaOffset, DateTimeKind.Utc);
}
