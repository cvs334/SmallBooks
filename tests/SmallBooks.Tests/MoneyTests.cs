using SmallBooks.Core;

namespace SmallBooks.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("1234.5", 123450)]
    [InlineData("1,234.50", 123450)]
    [InlineData("$1,234.50", 123450)]
    [InlineData(" 12 ", 1200)]
    [InlineData("(12.00)", -1200)]
    [InlineData("-0.01", -1)]
    [InlineData(".5", 50)]
    [InlineData("", 0)]
    public void Parses_what_people_type(string text, long cents)
    {
        Assert.True(Money.TryParse(text, out var money));
        Assert.Equal(cents, money.Cents);
    }

    [Theory]
    [InlineData("12.345")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("$")]
    public void Refuses_rather_than_guesses(string text) => Assert.False(Money.TryParse(text, out _));

    [Fact]
    public void Adds_without_rounding_error()
    {
        var total = Money.Zero;
        for (var i = 0; i < 10; i++) total += Money.Parse("0.10");
        Assert.Equal(Money.Parse("1.00"), total);
    }

    [Fact]
    public void Formats_for_screens_and_reports()
    {
        Assert.Equal("1,234.50", new Money(123450).ToString());
        Assert.Equal("$1,234.50", new Money(123450).ToCurrencyString());
        Assert.Equal("($5.00)", new Money(-500).ToCurrencyString());
    }

    [Fact]
    public void Rounds_half_away_from_zero() =>
        Assert.Equal(3, Money.FromDecimal(0.025m).Cents);
}
