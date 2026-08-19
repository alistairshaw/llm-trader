namespace Trading.Core.Identifiers;

public interface IIdentifierGenerator<out TIdentifier>
    where TIdentifier : notnull
{
    TIdentifier Generate();
}
