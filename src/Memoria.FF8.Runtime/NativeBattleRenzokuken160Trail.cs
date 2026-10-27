using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokuken160Controller
{
    public static NativeBattleRenzokuken160ChildStep Advance5FFAA0(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        if (counter < 0)
        {
            throw new NotSupportedException("Native5FFAA0 signed negative ring index leaves its reached history bank.");
        }
        NativeBattleRenzokuken160State state = State(frame);
        NativeBattleRenzokuken160WeaponTip bone = (inputs.ReadWeaponTip ?? throw new NotSupportedException("Native5FFAA0 requires its current5FFEE0 linked weapon bone1 position, direction and retained scratch padding."))(state.Actor);
        NativeBattleVector end = new(unchecked((short)(bone.Position.X + (unchecked(666 * bone.DirectionX) >> 12))), unchecked((short)(bone.Position.Y + (unchecked(666 * bone.DirectionY) >> 12))), unchecked((short)(bone.Position.Z + (unchecked(666 * bone.DirectionZ) >> 12))));
        byte[] history = state.TrailHistory.ToArray();
        int ring = counter % 7;
        int record = ring * 20;
        Dword(history, record, 1);
        Write(history, record + 4, bone.Position.X);
        Write(history, record + 6, bone.Position.Y);
        Write(history, record + 8, bone.Position.Z);
        ushort unknown = state.TrailHistoryUnknownPaddingMask;
        if (bone.PositionPadding is ushort positionPadding)
        {
            Write(history, record + 10, positionPadding);
            unknown = (ushort)(unknown & ~(1 << (ring * 2)));
        }
        else
        {
            unknown = (ushort)(unknown | (1 << (ring * 2)));
        }
        Write(history, record + 12, end.X);
        Write(history, record + 14, end.Y);
        Write(history, record + 16, end.Z);
        if (bone.EndpointPadding is ushort endpointPadding)
        {
            Write(history, record + 18, endpointPadding);
            unknown = (ushort)(unknown & ~(1 << (ring * 2 + 1)));
        }
        else
        {
            unknown = (ushort)(unknown | (1 << (ring * 2 + 1)));
        }
        NativeBattleEffectArena gathered = NativeBattleEffectArena.Clear();
        int count = 0;
        int admitted = counter < state.Duration - 7 ? 7 : state.Duration - counter;
        for (int index = ring; count < admitted; index = index == 0 ? 6 : index - 1)
        {
            int source = index * 20;
            if (BinaryPrimitives.ReadInt32LittleEndian(history.AsSpan(source)) == 0)
            {
                break;
            }
            gathered = gathered.Write(NativeBattleGilgameshSwordTrailState.GatherA + count * 8, history.AsSpan(source + 4, 8));
            gathered = gathered.Write(NativeBattleGilgameshSwordTrailState.GatherB + count * 8, history.AsSpan(source + 12, 8));
            if ((unknown & (1 << (index * 2))) != 0)
            {
                gathered = gathered.MarkUnknown(NativeBattleGilgameshSwordTrailState.GatherA + count * 8 + 6, 2);
            }
            if ((unknown & (1 << (index * 2 + 1))) != 0)
            {
                gathered = gathered.MarkUnknown(NativeBattleGilgameshSwordTrailState.GatherB + count * 8 + 6, 2);
            }
            count++;
        }
        state = state with { TrailHistory = history.ToImmutableArray(), TrailHistoryUnknownPaddingMask = unknown };
        if (count > 1)
        {
            NativeBattleEffectArena curve = gathered.Write(NativeBattleGilgameshSwordTrailState.CurveA, Bytes(state.TrailInner)).Write(NativeBattleGilgameshSwordTrailState.CurveB, Bytes(state.TrailOuter));
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, curve, NativeBattleGilgameshSwordTrailState.GatherA, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
            for (int index = 0; index < 32; index++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, curve, NativeBattleGilgameshSwordTrailState.CurveA + index * 8);
            }
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, curve, NativeBattleGilgameshSwordTrailState.GatherB, work);
            for (int index = 0; index < 32; index++)
            {
                curve = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, curve, NativeBattleGilgameshSwordTrailState.CurveB + index * 8);
            }
            byte[] outer = curve.Read(NativeBattleGilgameshSwordTrailState.CurveB, 256);
            NativeBattleVector origin = new(Read(outer, 8), Read(outer, 10), Read(outer, 12));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(Read(outer, 0) - origin.X, Read(outer, 2) - origin.Y, Read(outer, 4) - origin.Z);
            state = state with { TrailInner = Words(curve.Read(NativeBattleGilgameshSwordTrailState.CurveA, 256)), TrailOuter = Words(outer), Origin = origin, OriginPadding = unchecked((ushort)Read(outer, 14)), Direction = new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z)) };
            frame = WriteState(frame, state);
            NativeBattleGilgameshSwordTrailPacketResult packets = NativeBattleGilgameshSwordTrailPackets.Emit(new(curve), view, projection, registers, colors, 0x3A903030);
            frame = inputs.Execute(frame, new NativeBattleRenzokuken160TrailDraw(packets.Batch, packets.PoolCursorAdvance));
        }
        else
        {
            frame = WriteState(frame, state);
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), unchecked((short)task.ReadWord(12)) >= state.Duration - 1 ? 2 : 0);
    }

    private static byte[] Bytes(ImmutableArray<short> words)
    {
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
        {
            Write(bytes, index * 2, words[index]);
        }
        return bytes;
    }

    private static ImmutableArray<short> Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => Read(bytes, index * 2)).ToImmutableArray();
}
