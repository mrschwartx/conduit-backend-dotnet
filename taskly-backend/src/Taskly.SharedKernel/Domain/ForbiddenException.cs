namespace Taskly.SharedKernel.Domain;

public sealed class ForbiddenException(string message) : DomainException(message, "FORBIDDEN");
