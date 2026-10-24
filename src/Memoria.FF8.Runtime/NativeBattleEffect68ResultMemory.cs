using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect68Results
{
    public static NativeBattleEffect68ResultStep BuildB67EF0(NativeBattleEffect68Frame frame, int actor, NativeBattleScanResultServices services)
    {
        NativeBattleScanProcess process = frame.Memory.Scan ?? throw new NotSupportedException("Effect68 requires its current shared Scan process.");
        NativeBattleScanResultMemory shared = process.Results;
        NativeBattleScanResultMemory working = shared with
        {
            Element8 = ReadOwner(frame.State, ResultElementAddress, 8),
            Pages32 = ReadOwner(frame.State, ResultPagesAddress, 32),
            Count = 0,
            Empty = frame.State.Read(ResultEmptyAddress, 1)[0]
        };
        working = NativeBattleScanResults.BuildB68390(working, actor, services, ResultEmptyAddress);
        NativeBattleEffect68State state = WriteOwner(frame.State, ResultElementAddress, working.Element8, 8);
        state = WriteOwner(state, ResultPagesAddress, working.Pages32, 32).WriteDword(ResultCountAddress, working.Count).Write(ResultEmptyAddress, [working.Empty]);
        NativeBattleScanResultMemory published = shared with { Scratch48 = working.Scratch48, Cursor = working.Cursor, Ring768 = working.Ring768 };
        frame = frame with { State = state, Memory = frame.Memory with { Scan = process with { Results = published } } };
        return new(frame, unchecked((byte)working.Count));
    }

    public static uint PageB68370(NativeBattleEffect68Frame frame, byte index) => frame.State.ReadDword(checked(ResultPagesAddress + (uint)(index * 4)));

    private static NativeBattleScanByteOwner ReadOwner(NativeBattleEffect68State state, uint address, int length)
    {
        (byte[] bytes, bool[] known) = state.ReadView(address, length).ReadSnapshot(0, length);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffect68State WriteOwner(NativeBattleEffect68State state, uint address, NativeBattleScanByteOwner source, int length)
    {
        NativeBattleEffectArena target = NativeBattleEffectArena.Clear().MarkUnknown(0, length);
        for (int index = 0; index < length; index++)
        {
            if (source.Known[index])
            {
                target = target.Write(index, [source.Bytes[index]]);
            }
        }
        return state.WriteView(address, target, length);
    }
}
