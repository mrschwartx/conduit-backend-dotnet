namespace Taskly.SharedKernel.Domain;

public sealed class AlreadyExistsException(string message) : DomainException(message, "ALREADY_EXISTS");
