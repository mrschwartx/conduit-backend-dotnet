namespace Taskly.Modules.Users.Domain;

public sealed class InvalidTokenException : Exception
{
    public InvalidTokenException(string message)
        : base(message)
    {
    }

    public InvalidTokenException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
