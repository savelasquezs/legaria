using System.ComponentModel.DataAnnotations;
using Legaria.API.Controllers;

namespace Legaria.UnitTests;

public sealed class BranchContractValidationTests
{
    [Fact]
    public void OptionalContactFieldsAcceptEmptyStringsAtApiBoundary()
    {
        var input = new BranchInputModel(
            "Santander",
            string.Empty,
            "   ",
            "Calle 108A # 77D-30",
            "05001");
        var validationResults = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            input,
            new ValidationContext(input),
            validationResults,
            validateAllProperties: true);

        Assert.True(valid);
        Assert.Empty(validationResults);
    }
}
