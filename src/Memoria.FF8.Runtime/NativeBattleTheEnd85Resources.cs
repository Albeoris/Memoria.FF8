using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTheEnd85Resources(NativeBattleEffectHeapAllocation Camera, ImmutableArray<byte> Script, ImmutableArray<NativeBattleEffectFileAsset> Files, ImmutableArray<NativeBattleGfAnimationSource> Banks, NativeBattleFullscreenOverlayProgram Overlay, uint DirectorAddress, uint DefaultColor, ImmutableDictionary<uint, uint> Sounds)
{
    public byte[] Read(uint address, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        foreach (NativeBattleGfAnimationSource bank in Banks)
        {
            if (address >= bank.Address && (ulong)address - bank.Address + (uint)length <= (uint)bank.Data.Length)
            {
                return bank.Data.AsSpan().Slice(checked((int)(address - bank.Address)), length).ToArray();
            }
        }
        throw new NotSupportedException("Native The End read leaves its supplied executable source banks.");
    }
    public NativeBattleGfAnimationSource Program(uint address)
    {
        NativeBattleGfAnimationSource bank = Banks.Single(bank => address >= bank.Address && (ulong)address - bank.Address < (uint)bank.Data.Length);
        return new(address, bank.Data.AsSpan().Slice(checked((int)(address - bank.Address))).ToArray().ToImmutableArray());
    }
}
