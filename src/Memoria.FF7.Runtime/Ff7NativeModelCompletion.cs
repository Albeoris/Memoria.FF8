namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeModelCompletionState(int Operation, byte[] Payload, bool Pending = true)
{
    public void Validate()
    {
        if (Operation is not (11 or 12 or 255) || Payload is null)
        {
            throw new InvalidDataException("Native model completion requires an admitted selector and its original payload.");
        }
    }
}
