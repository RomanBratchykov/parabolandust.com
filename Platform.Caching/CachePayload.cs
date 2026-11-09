namespace Platform.Caching;

internal sealed record CachePayload<T>(int Version, DateTimeOffset ExpiresAt, T Value);

internal sealed record MemoryValue<T>(CachePayload<T> Payload, int Bytes);
