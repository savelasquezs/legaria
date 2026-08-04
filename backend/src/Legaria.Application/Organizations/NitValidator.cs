namespace Legaria.Application.Organizations;

public sealed class NitValidator : INitValidator
{
    private static readonly int[] Weights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    public bool IsValid(string nit, int verificationDigit) =>
        nit.Length is >= 6 and <= 14 &&
        nit.All(char.IsAsciiDigit) &&
        verificationDigit is >= 0 and <= 9 &&
        CalculateVerificationDigit(nit) == verificationDigit;

    public int CalculateVerificationDigit(string nit)
    {
        if (string.IsNullOrWhiteSpace(nit) || nit.Length > Weights.Length || !nit.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("El NIT debe contener solamente dígitos.", nameof(nit));
        }

        var sum = 0;
        for (var index = nit.Length - 1; index >= 0; index--)
        {
            sum += (nit[index] - '0') * Weights[nit.Length - 1 - index];
        }

        var remainder = sum % 11;
        return remainder > 1 ? 11 - remainder : remainder;
    }
}
