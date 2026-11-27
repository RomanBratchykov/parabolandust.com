namespace Platform.Caching;

public interface ICacheAside
{
    Task<T?> GetOrCreateAsync<T>(string group, string operation, string identity,
        CachePolicy policy, Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken = default) where T : class;
}
