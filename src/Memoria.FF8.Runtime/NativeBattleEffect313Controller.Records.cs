using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Controller
{
    public static (NativeBattleEffect313Frame Frame, int Count) AdvanceRecords59CA70(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(RecordSpriteAddress)).Write(0x24, BitConverter.GetBytes((short)0));
        frame = PublishContext(frame, temporary, context, 180);
        int count = 0;
        for (int index = 0; index < RecordCount; index++)
        {
            uint address = RecordsAddress + (uint)(index * RecordStride);
            if ((frame.State.Read(address, 1)[0] & 1) == 0)
            {
                continue;
            }
            NativeBattleEffectArena record = frame.State.ReadView(address, RecordStride);
            short scale = Word313(record, 6);
            frame = SetupRecordSprite313(frame, CopyBytes(record, 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), scale, unchecked((short)-(scale >> 4)), inputs);
            context = Context(frame, temporary, 180).Write(4, record.Read(4, 2));
            frame = PublishContext(frame, temporary, context, 180);
            NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(313, frame.State.Storage, RecordsAddress, index) with { ResourceGeneration = RecordSpriteAddress } : null);
            frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect313DrawBatch(NativeBattleEffect313Lower.PreserveNativeDepths(draw.Batch), CursorGlobal));
            if ((inputs.ReadFlags(frame) & 0x201) != 0)
            {
                continue;
            }
            frame = frame with { State = frame.State.WriteWord(address + 4, unchecked((ushort)(frame.State.ReadWord(address + 4) + 1))) };
            if (Word313(Context(frame, temporary, 180), 0x28) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(address, 0) };
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                uint pointAddress = address + 8 + (uint)(axis * 2);
                uint velocityAddress = address + 16 + (uint)(axis * 2);
                short velocity = unchecked((short)frame.State.ReadWord(velocityAddress));
                frame = frame with { State = frame.State.WriteWord(pointAddress, unchecked((ushort)(frame.State.ReadWord(pointAddress) + velocity))).WriteWord(velocityAddress, unchecked((ushort)(velocity - (velocity >> 4)))) };
            }
            count++;
        }
        return (Release(frame, 180), count);
    }

    public static NativeBattleEffect313Frame BirthRecords313(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        for (int point = 0; point < 3; point++)
        {
            int slot = Enumerable.Range(0, RecordCount).FirstOrDefault(index => frame.State.ReadDword(RecordsAddress + (uint)(index * RecordStride)) == 0, -1);
            if (slot < 0)
            {
                continue;
            }
            uint address = RecordsAddress + (uint)(slot * RecordStride);
            if (inputs.CapturePresentation)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.State.Storage, RecordsAddress, slot);
            }
            frame = frame with { State = frame.State.WriteDword(address, 1).WriteWord(address + 4, 0) };
            int scale = 2304 + Roll313(ref frame) % 1280;
            NativeBattleActionMatrix matrix = CurrentCasterMatrix313(frame, inputs);
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, Vector(Static313(resources, 0xCD8D50 + (uint)(point * 8), 8)));
            NativeBattleEffectArena center = frame.State.ReadView(CenterAddress, 8);
            center = PutPoint(center, 0, new(unchecked((short)(Word313(center, 0) + offset.X)), unchecked((short)(Word313(center, 2) + offset.Y)), unchecked((short)(Word313(center, 4) + offset.Z))));
            NativeBattleVector raw = NativeBattleGfMatrices.RotateShort56C4F0(CurrentCasterMatrix313(frame, inputs), Vector(Static313(resources, 0xCD8D68 + (uint)(point * 8), 8)));
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(raw.X, raw.Y, raw.Z);
            int speed = 90 + Roll313(ref frame) % 110;
            frame = frame with { State = frame.State.WriteWord(address + 6, unchecked((ushort)scale)).WriteView(address + 8, center, 8).WriteWord(address + 16, unchecked((ushort)(direction.X * speed >> 12))).WriteWord(address + 18, unchecked((ushort)(direction.Y * speed >> 12))).WriteWord(address + 20, unchecked((ushort)(direction.Z * speed >> 12))) };
        }
        return frame;
    }

    private static NativeBattleEffect313Frame SetupRecordSprite313(NativeBattleEffect313Frame frame, NativeBattleEffectArena point, short scale, short depth, NativeBattleEffect313Inputs inputs)
    {
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), null, scale, depth) };
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        return frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } } };
    }

    private static short Word313(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena Static313(NativeBattleEffect313Resources resources, uint address, int length) => NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(address, length));
    private static NativeBattleActionMatrix CurrentCasterMatrix313(NativeBattleEffect313Frame frame, NativeBattleEffect313Inputs inputs)
    {
        (byte[] bytes, bool[] known) = inputs.ReadModelArena9C(frame, ModelAddress(checked((int)frame.State.ReadDword(CasterGlobal)))).ReadSnapshot(0x40, 32);
        return new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
    }
    private static int Roll313(ref NativeBattleEffect313Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort result) };
        return result;
    }
}
