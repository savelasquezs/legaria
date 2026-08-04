using System.ComponentModel.DataAnnotations;
using Legaria.API.Controllers;

namespace Legaria.IntegrationTests;

public sealed class AuthContractValidationTests
{
    [Theory]
    [InlineData(typeof(LoginInput))]
    [InlineData(typeof(EmailInput))]
    [InlineData(typeof(TokenInput))]
    [InlineData(typeof(ResetPasswordInput))]
    public void InputRecords_KeepValidationMetadataOnConstructorParameters(Type inputType)
    {
        var constructor = Assert.Single(inputType.GetConstructors());

        Assert.All(
            constructor.GetParameters(),
            parameter => Assert.NotEmpty(
                parameter.GetCustomAttributes(typeof(ValidationAttribute), inherit: true)));
        Assert.All(
            inputType.GetProperties(),
            property => Assert.Empty(
                property.GetCustomAttributes(typeof(ValidationAttribute), inherit: true)));
    }
}
