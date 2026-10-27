using System.Runtime.InteropServices;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco97Uv
{
    public static ImmutableArray<byte> Scroll72DF60(ImmutableArray<byte> mesh, int deltaU) => Scroll72DF60(mesh, 0, deltaU);

    public static ImmutableArray<byte> Scroll72DF60(ImmutableArray<byte> owner, int resourceOffset, int deltaU) => Scroll(owner, resourceOffset, deltaU, false);

    public static ImmutableArray<byte> Scroll7311C0(ImmutableArray<byte> mesh, int deltaV) => Scroll7311C0(mesh, 0, deltaV);

    public static ImmutableArray<byte> Scroll7311C0(ImmutableArray<byte> owner, int resourceOffset, int deltaV) => Scroll(owner, resourceOffset, deltaV, true);

    private static ImmutableArray<byte> Scroll(ImmutableArray<byte> owner, int resourceOffset, int delta, bool vertical)
    {
        if (owner.IsDefault || resourceOffset < 0 || resourceOffset > owner.Length - 4)
        {
            throw new InvalidDataException("Native Choco UV mutation requires its complete current resource owner.");
        }
        NativeBattleEffectArena output = NativeBattleEffectArena.CreateUnknown(owner.Length).Write(0, owner.AsSpan());
        ScrollWorking(output, resourceOffset, delta, vertical);
        return ImmutableCollectionsMarshal.AsImmutableArray(output.Read(0, owner.Length));
    }

    internal static NativeBattleGfAnimationSource Scroll7311C0(NativeBattleGfAnimationSource source, int resourceOffset, int deltaV)
    {
        NativeBattleOwnedGfAnimationSource owner = NativeBattleOwnedGfAnimationSource.Own(source);
        using NativeBattleOwnedGfAnimationSource.Mutation mutation = owner.BeginMutation();
        ScrollWorking(mutation.View, resourceOffset, deltaV, true);
        mutation.Commit();
        return owner;
    }

    private static void ScrollWorking(NativeBattleEffectArena output, int resourceOffset, int delta, bool vertical)
    {
        if (resourceOffset < 0 || resourceOffset > output.LogicalLength - 4)
        {
            throw new InvalidDataException("Native Choco UV mutation requires its complete current resource owner.");
        }
        int directory = output.ReadInt32(resourceOffset);
        int cursor = checked(resourceOffset + directory / 4 * 4);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            Require(cursor, 4);
            int count = output.ReadInt32(cursor);
            cursor = checked(cursor + 4);
            int stride = strides[group];
            if (count < 0 || count > (output.LogicalLength - cursor) / stride)
            {
                throw new InvalidDataException("Native Choco UV group leaves its supplied current resource extent.");
            }
            int[] corners = group is 2 or 6 ? [10, 12, 16] : group is 3 or 7 ? [12, 16, 20, 22] : [];
            for (int face = 0; face < count; face++)
            {
                if (corners.Length != 0)
                {
                    uint[] candidates = corners.Select(offset => vertical ? unchecked((uint)((output.ReadByte(cursor + offset + 1) << 8) + (delta << 8))) : unchecked((uint)((output.ReadByte(cursor + offset) & 0x7F) + delta))).ToArray();
                    uint limit = vertical ? 0xFF00u : 0x7Fu;
                    bool wrap = candidates.Any(value => value > limit);
                    for (int corner = 0; corner < corners.Length; corner++)
                    {
                        uint candidate = wrap ? unchecked(candidates[corner] - (vertical ? 0x8000u : 64u)) : candidates[corner];
                        output.Write(cursor + corners[corner] + (vertical ? 1 : 0), [(byte)((candidate & limit) >> (vertical ? 8 : 0))]);
                    }
                }
                cursor = checked(cursor + stride);
            }
        }

        void Require(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > output.LogicalLength - length)
            {
                throw new InvalidDataException("Native Choco UV cursor leaves its supplied resource owner.");
            }
        }
    }

    internal static ImmutableArray<byte> UpdateDrawableResource(NativeBattleEffectNodeBank bank, uint resource, int kind, byte sourceNodeIndex)
    {
        if (sourceNodeIndex != 0 || kind is not (4 or 5))
        {
            return bank.Data;
        }
        if (kind == 5)
        {
            throw new NotSupportedException("Native Choco kind5 resource update requires its own admitted source contract.");
        }
        if (resource < bank.Address || (ulong)resource >= (ulong)bank.Address + (uint)bank.Length)
        {
            throw new NotSupportedException("Native Choco resource mutation requires its retained same-bank writable owner.");
        }
        return Scroll72DF60(bank.Data, checked((int)(resource - bank.Address)), -8);
    }
}
