namespace Newsletters.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public sealed class EntityNotFoundException(string message) : DomainException(message);

public sealed class ConflictException(string message) : DomainException(message);
