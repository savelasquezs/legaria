namespace Legaria.Application.Configuration;

public sealed class FirebaseStorageOptions
{
    public string Bucket { get; set; } = string.Empty;
    public string Prefix { get; set; } = "employee-documents";
    public string? GoogleApplicationCredentialsPath { get; set; }
}
