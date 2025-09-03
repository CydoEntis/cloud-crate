namespace CloudCrate.Domain.Exceptions;

public class NegativeValueException : DomainValidationException
{
    public NegativeValueException(string propertyName)
        : base($"{propertyName} cannot be negative.")
    {
    }
}