namespace Taskly.SharedKernel.Domain;

public sealed class PayloadTooLargeException(string message) : DomainException(message, "PAYLOAD_TOO_LARGE");
