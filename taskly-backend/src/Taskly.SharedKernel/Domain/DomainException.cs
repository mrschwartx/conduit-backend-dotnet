namespace Taskly.SharedKernel.Domain;

public abstract class DomainException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}
