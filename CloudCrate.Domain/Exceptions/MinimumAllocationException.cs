namespace CloudCrate.Domain.Exceptions;

public class MinimumAllocationException : DomainValidationException
{
    public MinimumAllocationException(long minGb) : base($"Minimum allocation is {minGb} GB.") { }
}