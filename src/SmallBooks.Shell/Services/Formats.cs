using System.Globalization;

namespace SmallBooks.Shell.Services;

internal static class Formats
{
    public static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Date(DateOnly d) => d.ToString("MMM d, yyyy", Us);
    public static string Month(int month) => Us.DateTimeFormat.GetMonthName(month);
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.0} MB",
    };

    public static string Source(string module) => module == SmallBooks.Core.Journal.SourceModules.Core
        ? "Manual"
        : char.ToUpperInvariant(module[0]) + module[1..];

    /// <summary>The first day of the fiscal year that contains <paramref name="day"/>.</summary>
    public static DateOnly FiscalYearStart(DateOnly day, int startMonth)
    {
        var year = day.Month >= startMonth ? day.Year : day.Year - 1;
        return new DateOnly(year, startMonth, 1);
    }
}
