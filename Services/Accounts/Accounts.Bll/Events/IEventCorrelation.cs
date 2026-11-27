namespace Accounts.Bll.Events;

// The API supplies its normalized request correlation without coupling BLL to HTTP.
public interface IEventCorrelation
{
    string? Current { get; }
}
