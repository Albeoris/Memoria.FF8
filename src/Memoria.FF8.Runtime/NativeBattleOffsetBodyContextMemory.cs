namespace Memoria.FF8.Runtime;

internal static class NativeBattleOffsetBodyContextMemory
{
    internal static NativeBattleEffectArena Read(NativeBattleFileStaging staging, uint address, int length)
    {
        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, address, length);
        foreach ((uint field, NativeBattleResourcePointer? pointer) in staging.EnumerateResourcePointers())
        {
            if (field >= address && (ulong)field + 4 <= (ulong)address + (uint)length)
            {
                view = view.WriteResourcePointer(checked((int)(field - address)), pointer);
            }
        }
        return view;
    }

    internal static NativeBattleFileStaging Publish(NativeBattleFileStaging staging, uint address, NativeBattleEffectArena view, int length)
    {
        KeyValuePair<int, NativeBattleResourcePointer?>[] pointers = view.EnumerateResourcePointers().Where(entry => entry.Key >= 0 && entry.Key <= length - 4).ToArray();
        staging = NativeBattleTemporaryMemory.PublishView(staging, address, view, length);
        foreach ((int field, NativeBattleResourcePointer? pointer) in pointers)
        {
            staging = staging.SetResourcePointerMetadata(checked(address + (uint)field), pointer);
        }
        return staging;
    }

    internal static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int length)
    {
        return destination.CopyResourceSnapshot(source, sourceOffset, destinationOffset, length);
    }
}
