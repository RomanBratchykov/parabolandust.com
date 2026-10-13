using System.Data.Common;

namespace Accounts.Dal;

public interface IUnitOfWork : IAsyncDisposable
{
    DbConnection Connection { get; }
}
