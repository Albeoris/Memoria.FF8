using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Lower
{
    private static NativeBattleEffect174Step AdvanceHistory(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        int slot = age % 5;
        if (slot < 0)
        {
            throw new InvalidDataException("Effect174 history leaves its selected physical slots.");
        }
        int actor = checked((int)frame.State.ReadDword(0x2395E50));
        ImmutableArray<NativeBattlePackedMatrix32> current = inputs.ReadCurrentBoneMatrices32(frame, actor);
        if (current.IsDefault || current.Length > 32)
        {
            throw new InvalidDataException("Effect174 history leaves its actual packed matrix capacity.");
        }
        int length = checked(current.Length * 32);
        uint sampleAddress = 0x2395E58 + (uint)(slot * 0x404);
        frame = frame with { State = frame.State.WriteDword(sampleAddress, 1) };
        frame = L174SaveBones(frame, sampleAddress + 4, current);
        frame = L174SaveBones(frame, 0x2394BA0, current);
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, actor);
        NativeBattleEffectArena originalMask = CopyBytes(model, 0x7C, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4);
        NativeBattleEffectArena originalColor = CopyBytes(model, 0x28, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0, 4);
        frame = L174WriteModel(frame, actor, 0x7C, L174Put(NativeBattleEffectArena.Clear(), 0, 8), 4, inputs);
        int previous = age < 1 ? 0 : (age - 1) % 5;
        int samples = age >= 61 ? 66 - age : 5;
        for (int index = 0; index < samples; index++)
        {
            uint history = 0x2395E58 + (uint)(previous * 0x404);
            if (frame.State.ReadDword(history) == 0)
            {
                break;
            }
            frame = inputs.WriteCurrentBoneMatrices32(frame, actor, L174ReadBones(frame, history + 4, length));
            frame = L174WriteModel(frame, actor, 0x28, L174Put(NativeBattleEffectArena.Clear(), 0, resources.HistoryColors[index]), 4, inputs);
            frame = inputs.Execute(frame, new NativeBattleEffect174DrawModelHistory5120(actor));
            previous--;
            if (previous < 0)
            {
                previous = 4;
            }
        }
        frame = inputs.WriteCurrentBoneMatrices32(frame, actor, L174ReadBones(frame, 0x2394BA0, length));
        frame = L174WriteModel(frame, actor, 0x7C, originalMask, 4, inputs);
        frame = L174WriteModel(frame, actor, 0x28, originalColor, 4, inputs);
        return L174Age(frame, address, 66, inputs);
    }

    private static NativeBattleEffect174Frame L174SaveBones(NativeBattleEffect174Frame frame, uint address, ImmutableArray<NativeBattlePackedMatrix32> matrices)
    {
        for (int index = 0; index < matrices.Length; index++)
        {
            frame = frame with { State = frame.State.WriteView(address + (uint)(index * 32), PackedArena(matrices[index]), 32) };
        }
        return frame;
    }

    private static ImmutableArray<NativeBattlePackedMatrix32> L174ReadBones(NativeBattleEffect174Frame frame, uint address, int length)
    {
        ImmutableArray<NativeBattlePackedMatrix32>.Builder matrices = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(length / 32);
        for (int offset = 0; offset < length; offset += 32)
        {
            NativeBattleEffectArena source = frame.State.ReadView(address + (uint)offset, 32);
            (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 32);
            matrices.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }
        return matrices.ToImmutable();
    }
}
