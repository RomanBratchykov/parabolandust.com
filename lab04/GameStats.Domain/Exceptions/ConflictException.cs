namespace GameStats.Domain.Exceptions;

// конфлікт версій або дублікат -> 409
public class ConflictException(string message) : Exception(message);