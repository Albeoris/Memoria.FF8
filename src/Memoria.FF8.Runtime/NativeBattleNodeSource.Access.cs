namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleNodeSource
{
    public byte[] Read(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > Length - length)
        {
            throw new InvalidDataException("Native node read leaves its retained source window.");
        }
        byte[] bytes = new byte[length];
        ReadInto(offset, bytes);
        return bytes;
    }

    internal void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        if (offset < 0 || offset > Length - bytes.Length)
        {
            throw new InvalidDataException("Native node store leaves its retained source window.");
        }
        if (IsStaging)
        {
            using NativeBattleFileStagingTransaction child = NativeBattleFileStagingTransaction.CreatePrivateChild(StagingAuthority);
            child.View.Write(checked(StagingAddress + (uint)offset), bytes);
            child.Freeze(child.View);
        }
        else
        {
            (Arena ?? throw new InvalidOperationException("Native node store requires its arena authority.")).Write(offset, bytes);
        }
    }

    internal NativeBattleEffectArena ReadView(int offset, int length)
    {
        if (IsStaging)
        {
            throw new InvalidOperationException("Native node arena views require their addressed arena source.");
        }
        return NativeBattleEffectNodeContextMemory.Copy(storage, offset, NativeBattleEffectArena.CreateUnknown(length), 0, length);
    }

    internal void WriteView(int offset, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Arena ?? throw new InvalidOperationException("Native node arena stores require their addressed arena source."), offset, length);
    }
}
