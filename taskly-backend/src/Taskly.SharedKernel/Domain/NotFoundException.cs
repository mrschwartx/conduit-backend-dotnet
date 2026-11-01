namespace Taskly.SharedKernel.Domain;

public sealed class NotFoundException(string message) : DomainException(message, "NOT_FOUND");
