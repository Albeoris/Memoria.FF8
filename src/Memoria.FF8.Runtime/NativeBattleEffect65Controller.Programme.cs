using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    private static uint CommonCursor(NativeBattleEffect65Frame frame)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect65 requires its current ordinary packet process.");
        uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018u, "off_B6D094" => 0x199B018u, _ => throw new NotSupportedException("Effect65 requires its selected ordinary packet bank.") };
        return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect65 requires its current ordinary packet cursor.")));
    }

    private static NativeBattleEffect65Frame InitializeProgramme(NativeBattleEffect65Frame frame, uint workspace, int size, int index, NativeBattleEffect65Resources resources)
    {
        uint file = checked(frame.State.ReadDword(ArenaGlobal) + FileOffset);
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, file + (uint)(4 + index * 4), 4).Read(0, 4));
        NativeBattleGfAnimationSource source = NativeBattleEffect65Programme.ReadSource(frame, checked(file + offset), resources);
        NativeBattleGfAnimationWorkspace cache = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, size);
        cache = cache.Initialize7016B0(source, size);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
        return frame;
    }

    private static NativeBattleEffectArena Caller48(NativeBattleEffect65Frame frame, NativeBattleActionMatrix matrix, uint selectors, short depth, short? phase, uint cursor)
    {
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(selectors)).Write(0x24, BitConverter.GetBytes(cursor)).Write(0x28, BitConverter.GetBytes(depth)).Write(0x2C, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8)));
        return phase is short value ? caller.Write(0x2A, BitConverter.GetBytes(value)) : caller.MarkUnknown(0x2A, 2);
    }

    public static NativeBattleEffect65Frame DrawMainProgramme6E45D0(NativeBattleEffect65Frame frame, uint main, int relativeAge, int length, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleVector saved = Vector(frame.State.ReadView(CasterPointAddress, 8));
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = saved.X, Y = 0, Z = saved.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = Caller48(frame, matrix, 0x1342D2C, 0, unchecked((short)(relativeAge << 5)), frame.State.ReadDword(CursorGlobal));
        return NativeBattleEffect65Programme.Advance(frame, main + 0x48, length, caller, unchecked((int)frame.State.ReadDword(ControlGlobal)), resources, inputs).Frame;
    }

    public static NativeBattleEffect65Frame DrawMainProgramme6E4670(NativeBattleEffect65Frame frame, uint main, int length, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleVector saved = Vector(frame.State.ReadView(CasterPointAddress, 8));
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = saved.X, Y = 0, Z = saved.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        NativeBattleEffectArena caller = Caller48(frame, matrix, 0x133AB98, 0, null, frame.State.ReadDword(CursorGlobal));
        return NativeBattleEffect65Programme.Advance(frame, main + 0x48, length, caller, unchecked((int)frame.State.ReadDword(ControlGlobal)), resources, inputs).Frame;
    }

    public static NativeBattleEffect65Step AdvanceChild6E50A0(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        frame = frame with { State = frame.State.WriteDword(ScaleGlobal, task.ReadDword(0x14)) };
        NativeBattleEffectArena caller = Caller48(frame, matrix, 0x133AB98, Signed(task, 0x12), null, CommonCursor(frame));
        uint source = task.ReadDword(0x18);
        uint file = checked(frame.State.ReadDword(ArenaGlobal) + FileOffset);
        int index = Array.IndexOf(BundleProgrammeOffsets[394].ToArray(), checked((int)(source - file)));
        int length = index switch { 4 => 0x78, 5 => 0x1A0, _ => throw new InvalidDataException("Effect65 child workspace has an unsupported current programme pointer.") };
        NativeBattleEffect65Step step = NativeBattleEffect65Programme.Advance(frame, address + 0x18, length, caller, unchecked((int)frame.State.ReadDword(ControlGlobal)), resources, inputs, true, 0x1D8E054);
        return new(step.Frame, step.Result == 0 ? 2 : 0);
    }

    private static NativeBattleEffect65Frame DrawPairedProgrammes(NativeBattleEffect65Frame frame, uint main, int age, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        int start = age < 55 ? 42 : 55;
        int size = start == 42 ? 0x3AC : 0x1AC;
        if (age == start)
        {
            frame = InitializeProgramme(frame, main + 0x48, size, start == 42 ? 0 : 1, resources);
            frame = InitializeProgramme(frame, main + 0xA0C, size, start == 42 ? 0 : 1, resources);
        }
        NativeBattleActionMatrix local;
        if (start == 42)
        {
            int sin = NativeBattleCameraMath.Sine(-800);
            int cos = NativeBattleCameraMath.Cosine(-800);
            local = new([unchecked((short)(cos * 8192 >> 12)), 0, unchecked((short)-(sin * 8192 >> 12)), 0, 8192, 0, unchecked((short)(sin * 8192 >> 12)), 0, unchecked((short)(cos * 8192 >> 12))], 0, 0, 0, 4096);
        }
        else
        {
            local = NativeBattlePandemona291Restoration.Rotation701270(800);
        }
        for (int side = 0; side < 2; side++)
        {
            NativeBattleActionMatrix rotation = side == 0 ? local : local with { Rotation = local.Rotation.Select((value, index) => index < 3 ? unchecked((short)-value) : value).ToImmutableArray() };
            NativeBattleEffect65PointOutputStep point = inputs.ReadCasterPoint6E2CB0(frame, ReadSource(frame, resources, side == 0 ? 0x133ABB8u : 0x133ABB0u, 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 12));
            frame = point.Frame;
            NativeBattleActionMatrix placed = rotation with { X = BinaryPrimitives.ReadInt32LittleEndian(point.Output.Read(0, 4)), Y = BinaryPrimitives.ReadInt32LittleEndian(point.Output.Read(4, 4)), Z = BinaryPrimitives.ReadInt32LittleEndian(point.Output.Read(8, 4)) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), placed);
            NativeBattleEffectArena caller = Caller48(frame, matrix, 0x133AB98, 0, null, frame.State.ReadDword(CursorGlobal));
            frame = NativeBattleEffect65Programme.Advance(frame, main + (side == 0 ? 0x48u : 0xA0Cu), size, caller, unchecked((int)frame.State.ReadDword(ControlGlobal)), resources, inputs).Frame;
        }
        return frame;
    }
}
