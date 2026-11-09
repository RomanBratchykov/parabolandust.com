namespace GameStats.Domain.Exceptions;

// ресурс не знайдено -> 404
public class NotFoundException(string message) : Exception(message);