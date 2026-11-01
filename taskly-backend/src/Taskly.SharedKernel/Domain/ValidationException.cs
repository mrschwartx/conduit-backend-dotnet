namespace Taskly.SharedKernel.Domain;

public sealed class ValidationException(string message) : DomainException(message, "VALIDATION_ERROR");
