namespace CloudCrate.Domain.Exceptions;

public class InsufficientStorageException : DomainValidationException
{
    public InsufficientStorageException() : base("Insufficient allocated storage.")
    {
    }
}