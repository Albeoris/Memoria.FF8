namespace Memoria.FF8.Resources;

/// <summary>The current source dictionary used to expand indexed native text terms.</summary>
public interface ITextTermSource
{
    long Generation { get; }
    string Resolve(int index);
}
