using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMightyGuardMeshPackets
{
    public static NativeBattleEsunaMeshPreparation Prepare(NativeBattleModulePoolTask task, sbyte sourceIndex, short sourceWord3A, short fade, uint? farColor)
    {
        if (task.Callback != NativeBattleMightyGuardProfile.ForEffect(78).GenericCallback && task.Callback != NativeBattleMightyGuardProfile.ForEffect(73).GenericCallback)
        {
            throw new InvalidDataException("Mighty Guard's mesh wrapper requires its original generic context.");
        }
        NativeBattleEsunaMeshOperands prepared = Prepare(task.ReadWord(0x34 + 0x28), sourceIndex, sourceWord3A, fade, farColor);
        if (sourceIndex is 4 or 5)
        {
            task = task.WriteWord(0x34 + 0x28, prepared.NextUvCounter);
        }
        return new(task, prepared.Context);
    }

    public static NativeBattleEsunaMeshOperands Prepare(ushort counter, sbyte sourceIndex, short sourceWord3A, short fade, uint? farColor)
    {
        uint flags = sourceWord3A == 0 ? 0x30u : 0;
        if (fade != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleTextureWindow after = full;
        ushort next = counter;
        ushort horizontal = 0;
        if (sourceIndex is 4 or 5)
        {
            horizontal = unchecked((ushort)((unchecked((byte)counter) + 2) & 0x7F));
            next = horizontal;
            after = new(0, 0, 128, 256);
        }
        return new(next, new(flags, fade == 0 ? null : farColor ?? throw new NotSupportedException("Mighty Guard's fading mesh requires its actual far color."), fade == 0 ? null : fade, 0, horizontal, 0, full, after, 17, 2));
    }

    public static NativeBattleGfGeometryBatch EmitBatch(ImmutableArray<byte> mesh, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleEsunaMeshContext context, NativeBattleSummonColors? colors = null, Func<int, NativeBattleVector>? readSelectedVertex = null, Action<uint>? observeFirstThreeFlags = null)
    {
        return NativeBattleRestorationMeshPackets.EmitBatch88E240(mesh, matrix, projection, context, colors, readSelectedVertex, observeFirstThreeFlags);
    }
}
