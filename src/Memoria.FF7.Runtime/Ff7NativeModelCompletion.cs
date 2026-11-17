namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeModelCompletionState(int Operation, byte[] Payload, bool Pending = true)
{
    public void Validate()
    {
        if (Operation != 12 || Payload is null || Payload.Length == 0)
        {
            throw new InvalidDataException("Native model completion requires an admitted selector and its original payload.");
        }
    }
}
