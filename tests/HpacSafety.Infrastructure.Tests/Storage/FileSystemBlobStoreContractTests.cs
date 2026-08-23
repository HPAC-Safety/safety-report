using HpacSafety.Core.SharedKernel;
using HpacSafety.Infrastructure.Storage;

namespace HpacSafety.Infrastructure.Tests.Storage;

/// <summary>
/// The contract suite against the development store. No Docker, so it runs
/// everywhere — which is exactly why it must not be the only place the contract
/// is checked. See <see cref="MinioBlobStoreContractTests" />.
/// </summary>
public sealed class FileSystemBlobStoreContractTests : BlobStoreContractTests, IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "hpac-blob-tests", Guid.NewGuid().ToString("n"));
    private FileSystemBlobStore _store = null!;

    protected override Task<IBlobStore> CreateStoreAsync()
    {
        _store = new FileSystemBlobStore(new FileSystemBlobStoreOptions { RootPath = _root }, TimeProvider.System);
        return Task.FromResult<IBlobStore>(_store);
    }

    protected override async Task<bool> TryUploadAsync(Uri uploadUrl, byte[] content, string contentType)
    {
        try
        {
            using var source = new MemoryStream(content);
            await _store.ExecuteUploadAsync(uploadUrl, source, CancellationToken.None);
            return true;
        }
        catch (PresignedUrlRejectedException)
        {
            return false;
        }
    }

    protected override async Task<bool> TryReadAsync(Uri readUrl)
    {
        try
        {
            await using var stream = await _store.ExecuteReadAsync(readUrl, CancellationToken.None);
            return true;
        }
        catch (PresignedUrlRejectedException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    protected override Uri RetargetToKey(Uri url, BlobKey key) =>
        new(new UriBuilder(url) { Path = "/" + key.Value }.Uri.ToString());

    // The store reports a missing blob as FileNotFoundException, which is what
    // the contract suite's existence check expects.

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
