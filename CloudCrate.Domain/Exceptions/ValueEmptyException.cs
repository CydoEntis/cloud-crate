namespace CloudCrate.Domain.Exceptions;

public class ValueEmptyException : DomainValidationException
{
    public ValueEmptyException(string propertyName)
        : base($"{propertyName} cannot be empty.") { }
}