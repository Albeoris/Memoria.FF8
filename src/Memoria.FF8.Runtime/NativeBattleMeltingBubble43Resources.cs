using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltingBubble43Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, NativeBattleModuleResource StaticData, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 296 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 296 || Executable is null || Executable.ResourceAddress != 0x15C6080 || Executable.Data.IsDefault || Executable.Data.Length != 260 || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 1 || StaticData is null || StaticData.Address != 0x15C3684 || StaticData.Bytes.IsDefault || StaticData.Bytes.Length != 10748 || Sound != 0 || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan().Slice(256)) != Sound)
        {
            throw new InvalidDataException("Melting Bubble requires its original flag1 directory, backwards static resources, camera and aliased sound descriptor.");
        }
        uint[] addresses = [0x15C3684, 0x15C38B4, 0x15C3AE4, 0x15C3F44, 0x15C40F0, 0x15C429C, 0x15C4448, 0x15C45F4];
        int[] lengths = [560, 560, 560, 428, 428, 428, 428, 40];
        int[] frames = [21, 21, 21, 16, 16, 16, 16, 1];
        if (Sprites.IsDefault || Sprites.Length != addresses.Length)
        {
            throw new InvalidDataException("Melting Bubble requires its eight distinct original sprite resources.");
        }
        for (int index = 0; index < addresses.Length; index++)
        {
            NativeBattleModuleResource sprite = Sprites[index];
            if (sprite is null || sprite.Address != addresses[index] || sprite.Bytes.IsDefault || sprite.Bytes.Length != lengths[index] || BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) != frames[index] || !sprite.Bytes.AsSpan().SequenceEqual(ReadStatic(sprite.Address, sprite.Bytes.Length)))
            {
                throw new InvalidDataException("Melting Bubble sprite bytes contradict their original addressed immutable owner.");
            }
            for (int frame = 0; frame < frames[index]; frame++)
            {
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, Sound) { Effect = 43, Executable = Executable };
    }

    public byte[] ReadStatic(uint address, int length)
    {
        if (StaticData is null || StaticData.Bytes.IsDefault || length < 0 || address < StaticData.Address || (ulong)address + (uint)length > (ulong)StaticData.Address + (uint)StaticData.Bytes.Length)
        {
            throw new InvalidDataException("Melting Bubble immutable reads leave their actual backwards resource span.");
        }
        return StaticData.Bytes.AsSpan().Slice(checked((int)(address - StaticData.Address)), length).ToArray();
    }

    public ImmutableArray<byte> ReadResource(uint address)
    {
        NativeBattleModuleResource? resource = Sprites.IsDefault ? null : Sprites.SingleOrDefault(sprite => sprite.Address == address);
        return resource?.Bytes ?? throw new NotSupportedException("Melting Bubble sprite address leaves its original resource map.");
    }
}
