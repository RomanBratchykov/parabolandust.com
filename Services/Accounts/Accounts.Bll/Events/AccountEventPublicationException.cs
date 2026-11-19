namespace Accounts.Bll.Events;

public sealed class AccountEventPublicationException(Exception innerException)
    : Exception("The account change committed, but its integration event could not be published.", innerException);
