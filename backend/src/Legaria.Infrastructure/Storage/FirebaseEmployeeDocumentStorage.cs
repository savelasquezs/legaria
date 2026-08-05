using Google;
using Google.Cloud.Storage.V1;
using Legaria.Application.Configuration;
using Legaria.Application.Documents;
using Microsoft.Extensions.Options;

namespace Legaria.Infrastructure.Storage;

public sealed class FirebaseEmployeeDocumentStorage(IOptions<FirebaseStorageOptions> options) : IEmployeeDocumentStorage
{
    private readonly FirebaseStorageOptions _options = options.Value;
    private StorageClient? _client;
    private StorageClient Client => _client ??= StorageClient.Create();

    public async Task<string> UploadAsync(Stream content, string extension, string contentType, CancellationToken cancellationToken)
    {
        EnsureReady();
        var prefix = _options.Prefix.Trim().Trim('/');
        var objectName = $"{(string.IsNullOrWhiteSpace(prefix) ? "employee-documents" : prefix)}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        await Client.UploadObjectAsync(_options.Bucket.Trim(), objectName, contentType, content, cancellationToken: cancellationToken);
        return objectName;
    }

    public async Task<Stream> DownloadAsync(string objectName, CancellationToken cancellationToken)
    {
        EnsureReady();
        var stream = new MemoryStream();
        await Client.DownloadObjectAsync(_options.Bucket.Trim(), objectName, stream, cancellationToken: cancellationToken);
        stream.Position = 0;
        return stream;
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken)
    {
        EnsureReady();
        try { await Client.DeleteObjectAsync(_options.Bucket.Trim(), objectName, cancellationToken: cancellationToken); }
        catch (GoogleApiException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound) { return; }
    }

    private void EnsureReady()
    {
        if (string.IsNullOrWhiteSpace(_options.Bucket)) throw new InvalidOperationException("Falta FIREBASE_STORAGE_BUCKET.");
    }
}
