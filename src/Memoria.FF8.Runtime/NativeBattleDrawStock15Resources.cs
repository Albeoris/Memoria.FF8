using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleCuragaExecutableSource Executable, NativeBattleDrawStock15ParticleResources Particles)
{
    public void Validate()
    {
        if (Animation is null || Animation.Data.IsDefault || Animation.Data.Length != 0x7CC || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan()) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(Animation.Data.AsSpan().Slice(6)) != Animation.Data.Length || Executable is null || Executable.ResourceAddress != NativeBattleDrawStock15Profile.ExecutableAddress || Executable.Data.IsDefault || Executable.Data.Length != NativeBattleDrawStock15Profile.ExecutableLength || BinaryPrimitives.ReadUInt32LittleEndian(Executable.Data.AsSpan()) != 0 || Particles is null)
        {
            throw new InvalidDataException("Draw Stock requires its complete original camera, executable bank and particle resources.");
        }
        Particles.Validate();
    }

    public NativeBattleCuragaResources GenericNodes()
    {
        return new(Animation, 0) { Effect = 15, Executable = Executable };
    }
}
