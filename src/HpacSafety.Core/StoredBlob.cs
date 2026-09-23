namespace HpacSafety.Core;

/// <summary>What storage knows about one object: the type it was written as and its size.</summary>
/// <param name="ContentType">The content type the object was stored with.</param>
/// <param name="ByteSize">Its size in bytes.</param>
public sealed record StoredBlob(string ContentType, long ByteSize);
