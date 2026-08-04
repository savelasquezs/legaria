using Legaria.Application.Organizations;

namespace Legaria.UnitTests;

public sealed class NitValidatorTests
{
    private readonly NitValidator _validator = new();

    [Theory]
    [InlineData("900373913", 4)]
    [InlineData("800197268", 4)]
    [InlineData("860002964", 4)]
    [InlineData("899999068", 1)]
    public void ValidatesKnownNitAndVerificationDigit(string nit, int digit)
    {
        Assert.Equal(digit, _validator.CalculateVerificationDigit(nit));
        Assert.True(_validator.IsValid(nit, digit));
    }

    [Theory]
    [InlineData("900.373.913", 4)]
    [InlineData("900373913-4", 4)]
    [InlineData("12345", 1)]
    [InlineData("123456", 9)]
    public void RejectsPunctuationLengthAndWrongDigit(string nit, int digit) =>
        Assert.False(_validator.IsValid(nit, digit));
}
