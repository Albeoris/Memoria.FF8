namespace Memoria.FF8.Resources;

/// <summary>A selected replacement cannot serve its resource; other resources may still be published.</summary>
public class ResourceReplacementRejectedException(string message, Exception? innerException = null) : IOException(message, innerException)
{
}
