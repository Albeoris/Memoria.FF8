using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect107Resources(ImmutableArray<byte> ExecutableBank, NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleModuleResource> Sprites, uint Sound)
{
    public const uint Address = 0x12EC358;
    public const int Length = 0xD6A4;

    public ImmutableArray<byte> Read(uint address, int length)
    {
        long offset = address - (long)Address;
        if (ExecutableBank.IsDefault || ExecutableBank.Length != Length || offset < 0 || length < 0 || offset > ExecutableBank.Length - length)
        {
            throw new InvalidDataException("Native effect107 resource access leaves its authenticated executable bank.");
        }
        return ExecutableBank.AsSpan().Slice((int)offset, length).ToArray().ToImmutableArray();
    }

    public NativeBattleGfAnimationSource Hierarchy => new(0x12EC9D4, Read(0x12EC9D4, 0x6694));
}
