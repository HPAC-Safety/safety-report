using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace HpacSafety.Infrastructure.Persistence.Encryption;

/// <summary>
/// Keeps two contexts holding different encryption keys from sharing one cached
/// model.
/// </summary>
/// <remarks>
/// EF Core caches the model per context type. The value converter on an
/// encrypted column closes over a cipher, so a cached model carries the key it
/// was built with — and a second context opened with a different key would
/// silently read through the first one's. That is exactly the failure the
/// "unreadable without the key" test exists to catch, so the key identifier is
/// part of the cache key. See ADR-0019.
/// </remarks>
public sealed class FieldCipherModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime) =>
        context is HpacSafetyDbContext hpac
            ? (context.GetType(), hpac.CipherKeyId, designTime)
            : (object)(context?.GetType() ?? typeof(DbContext), designTime);
}
