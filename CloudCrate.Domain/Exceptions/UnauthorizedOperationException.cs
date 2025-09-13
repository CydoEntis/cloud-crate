namespace CloudCrate.Domain.Exceptions;

public class UnauthorizedOperationException : DomainValidationException
{
    public UnauthorizedOperationException(string message) : base(message)
    {
    }
}