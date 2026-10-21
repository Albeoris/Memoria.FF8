using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindBlast29Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, uint Sound)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 276 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != 276 || Executable is null || Executable.ResourceAddress != 0x17D27DC || Executable.Data.IsDefault || Executable.Data.Length != 14192 || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0 || Sound != 0x61B65)
        {
            throw new InvalidDataException("Wind Blast requires its complete original camera, uninitialized node bank and sound.");
        }
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, Sound) { Effect = 29, Executable = Executable };
    }
}
