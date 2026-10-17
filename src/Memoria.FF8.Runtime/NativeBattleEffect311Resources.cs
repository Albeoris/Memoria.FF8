using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect311Resources(ImmutableArray<byte> ExecutableBank, NativeBattleEffectHeapAllocation Animation, NativeBattleModuleResource Sprite, uint Sound)
{
    public const uint Address = 0x101AF28;
    public const int Length = 0x82A0;
    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = address - (long)Address;
        if (ExecutableBank.IsDefault || ExecutableBank.Length != Length || offset < 0 || length < 0 || offset > ExecutableBank.Length - length)
        {
            throw new InvalidDataException("Native effect311 resource access leaves its authenticated executable bank.");
        }
        return ExecutableBank.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }
    public NativeBattleGfAnimationSource Hierarchy => new(0x101CEB8, Read(0x101CEB8, 0x3E68));
}

public sealed record NativeBattleEffect311State(ImmutableArray<byte> Mesh);
