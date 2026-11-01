namespace Taskly.SharedKernel.Domain;

public sealed class AuthenticationFailedException(string message) : DomainException(message, "AUTHENTICATION_FAILED");
