namespace GameStats.Domain.Exceptions;

// порушення бізнес-правила в сутності -> 400
public class DomainException(string message) : Exception(message);