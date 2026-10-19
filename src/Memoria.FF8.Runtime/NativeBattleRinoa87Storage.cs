using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa87Storage
{
    public static NativeBattleRinoa87State FromColdProcess() => new(0, 0, null, null, null) { BodyProjected = new byte[2560].ToImmutableArray(), MorphScratch = new byte[1120].ToImmutableArray() };

    public static void Validate(NativeBattleRinoa87State state)
    {
        if (state.SourceArenaOffset < 0 || state.SourceArenaOffset > NativeBattleEffectArena.Length - 81920 || state.AuxiliaryCursor < 0 || state.AuxiliaryCursor > NativeBattleEffectArena.Length || state.BodyProjected.IsDefault || state.BodyProjected.Length != 2560 || state.MorphScratch.IsDefault || state.MorphScratch.Length != 1120 || state.BodyBones is { } bones && (bones.IsDefault || bones.Length != 34) || state.BodyModel is not null && state.BodyBones is null)
        {
            throw new InvalidDataException("Native Rinoa87 retained storage lost its original physical workspace or body owner.");
        }
        if (state.NodeData is { } node && (node.IsDefault || node.Length != NativeBattleRinoa87NodeResources.Length || BinaryPrimitives.ReadUInt32LittleEndian(node.AsSpan()) is not (0 or 1)))
        {
            throw new InvalidDataException("Native Rinoa87 node source contradicts its retained physical owner.");
        }
        state.Scene?.Validate();
        state.Io?.Validate();
        state.CopiedCamera?.Validate();
        if (state.BodyBones is { } matrices)
        {
            foreach (NativeBattleActionMatrix matrix in matrices)
            {
                matrix.Validate();
            }
        }
    }
}
