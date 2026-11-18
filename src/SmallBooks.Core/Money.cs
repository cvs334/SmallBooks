using System.Globalization;

namespace SmallBooks.Core;

/// <summary>
/// An amount of money in the company's currency, held as whole cents so that
/// adding and comparing amounts never suffers from rounding error.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public decimal Amount => Cents / 100m;
    public bool IsZero => Cents == 0;
    public bool IsNegative => Cents < 0;

    /// <summary>Creates an amount from a decimal, rounding half away from zero to the nearest cent.</summary>
    public static Money FromDecimal(decimal amount) =>
        new((long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public static Money operator -(Money a) => new(checked(-a.Cents));
    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
    public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;
    public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

    public Money Abs() => new(Math.Abs(Cents));

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    /// <summary>Formats as 1,234.56 (no currency symbol); negatives show a leading minus.</summary>
    public override string ToString() => Amount.ToString("#,##0.00", Us);

    /// <summary>Formats as $1,234.56, or ($1,234.56) for negatives, as reports usually show them.</summary>
    public string ToCurrencyString() =>
        IsNegative ? "(" + (-this).Amount.ToString("$#,##0.00", Us) + ")" : Amount.ToString("$#,##0.00", Us);

    /// <summary>
    /// Reads what a person typed: "1234.5", "1,234.50", "$1,234.50", "(12.00)" or "-12".
    /// Blank text reads as zero. More than two decimal places is refused rather than rounded.
    /// </summary>
    public static bool TryParse(string? text, out Money money)
    {
        money = Zero;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var s = text.Trim();
        var negative = false;
        if (s.StartsWith('(') && s.EndsWith(')'))
        {
            negative = true;
            s = s[1..^1].Trim();
        }
        s = s.Replace("$", "").Replace(",", "").Trim();
        if (s.StartsWith('-'))
        {
            negative = !negative;
            s = s[1..].Trim();
        }
        if (s.Length == 0) return false;

        if (!decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return false;
        if (decimal.Round(value, 2) != value) return false;

        try
        {
            money = FromDecimal(negative ? -value : value);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static Money Parse(string text) =>
        TryParse(text, out var m) ? m : throw new FormatException($"'{text}' is not an amount of money.");

    public static Money Sum(IEnumerable<Money> values)
    {
        long total = 0;
        foreach (var v in values) total = checked(total + v.Cents);
        return new Money(total);
    }
}
