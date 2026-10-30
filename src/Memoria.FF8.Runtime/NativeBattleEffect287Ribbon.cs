using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect287Ribbon
{
    public static NativeBattleEffect287Frame Draw41C0(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleEffectArena rows = ReadBytes(frame, address, 128);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Ribbon requires its current projection controls.");
        System.Numerics.Vector3[]? world = pilotIdentity is null ? null : new System.Numerics.Vector3[8];
        uint[]? centers = pilotIdentity is null ? null : new uint[8];
        for (int index = 0; index < 8; index++)
        {
            NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(rows, index * 16, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            registers = NativeBattleEffect287Attachments.LoadPacked(registers, point);
            if (world is not null)
            {
                NativeBattleActionMatrix matrix = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(registers.Geometry).ReadMatrix();
                world[index] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, new(Word287(point, 0), Word287(point, 2), Word287(point, 4))), inputs.ReadProjection(frame));
            }
            registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            if (centers is not null)
            {
                centers[index] = registers.Geometry.Sxy[2]!.Value;
            }
            rows = Put287(rows, index * 16, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Ribbon requires its current projected point."));
            rows = PutWord287(rows, index * 16 + 4, unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("Ribbon requires its current projected depth."))) >> 2);
        }
        frame = Publish(frame, registers);
        (rows, short? previousX, short? previousY) = Extrude4350(rows, 0, 1, false, null, null, resources);
        for (int index = 1; index < 7; index++)
        {
            (rows, previousX, previousY) = Extrude4350(rows, index, index + 1, true, previousX, previousY, resources);
        }
        (rows, _, _) = Extrude4350(rows, 7, null, true, previousX, previousY, resources);
        frame = WriteBytes(frame, address, rows, 128);
        NativeBattleGfGeometryBatch batch = Emit(rows, registers.Geometry.Sz, pilotIdentity: pilotIdentity, world: world, centers: centers);
        return inputs.Execute(frame, new NativeBattleEffect287DrawBatch(batch, 0x1D8E054));
    }

    public static (NativeBattleEffectArena Rows, short? PreviousX, short? PreviousY) Extrude4350(NativeBattleEffectArena rows, int index, int? next, bool hasPrevious, short? previousX, short? previousY, NativeBattleEffect287Resources resources)
    {
        int offset = index * 16;
        short depth = Word287(rows, offset + 4);
        if (depth < 0 || next is { } n && Word287(rows, n * 16 + 4) < 0)
        {
            return (rows, previousX, previousY);
        }
        int x = Word287(rows, offset);
        int y = Word287(rows, offset + 2);
        int offsetX = 0;
        int offsetY = 0;
        if (next is { } following)
        {
            int dx = Word287(rows, following * 16) - x;
            int dy = y - Word287(rows, following * 16 + 2);
            int tableIndex = Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127);
            int distance = resources.ReadStatic(NativeBattleEffect287Profile.DistanceAddress + (uint)tableIndex, 1)[0];
            NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(dx, dy, distance, hasPrevious, previousX, previousY);
            if (!direction.Valid)
            {
                return (PutWord287(rows, offset + 4, -1), previousX, previousY);
            }
            previousX = direction.PreviousX;
            previousY = direction.PreviousY;
            int width = 8320 / (depth + 1024);
            offsetX = unchecked(width * direction.X) >> 12;
            offsetY = unchecked(width * direction.Y) >> 12;
        }
        rows = PutWord287(PutWord287(PutWord287(PutWord287(rows, offset, x - offsetX), offset + 2, y - offsetY), offset + 8, x + offsetX), offset + 10, y + offsetY);
        return (rows, previousX, previousY);
    }

    public static NativeBattleGfGeometryBatch Emit(NativeBattleEffectArena rows, ImmutableArray<uint?> currentSz, NativeBattlePilotVisualIdentity? pilotIdentity = null, IReadOnlyList<System.Numerics.Vector3>? world = null, IReadOnlyList<uint>? centers = null)
    {
        ImmutableArray<int> payload = currentSz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Ribbon insertion requires its actual current depth FIFO.")))).ToImmutableArray();
        if (payload.Length != 4)
        {
            throw new InvalidDataException("Ribbon insertion requires four current depths.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 7; index++)
        {
            int first = index * 16;
            int second = first + 16;
            int firstDepth = Word287(rows, first + 4);
            int secondDepth = Word287(rows, second + 4);
            if (firstDepth <= 0 || secondDepth <= 0)
            {
                continue;
            }
            SkinWrites packet = new(52, allocated);
            packet.Word(0, 0x0C000000);
            uint firstColor = Gray(Word287(rows, first + 14));
            uint secondColor = Gray(Word287(rows, second + 14));
            packet.Word(4, firstColor);
            packet.Word(16, firstColor);
            packet.Word(28, secondColor);
            packet.Word(40, secondColor);
            packet.Byte(7, 0x3E);
            packet.Word(8, Dword287(rows, first));
            packet.Word(20, Dword287(rows, first + 8));
            packet.Word(32, Dword287(rows, second));
            packet.Word(44, Dword287(rows, second + 8));
            packet.Word(12, 0x3E14C048);
            packet.Word(24, 0x00B9C078);
            packet.Word16(36, 0xC048);
            packet.Word16(48, 0xC078);
            int bucket = unchecked(17 + ((firstDepth + secondDepth) >> 3));
            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload);
            if (pilotIdentity is { } identity)
            {
                if (world?.Count != 8 || centers?.Count != 8)
                {
                    throw new InvalidDataException("Ribbon capture requires its eight actually projected source rows.");
                }
                System.Numerics.Vector2 Offset(int row, int corner) => new System.Numerics.Vector2(Word287(rows, row * 16 + corner), Word287(rows, row * 16 + corner + 2)) - new System.Numerics.Vector2(unchecked((short)centers[row]), unchecked((short)(centers[row] >> 16)));
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + index) }, world[index], world[index], world[index + 1], world[index + 1], 4) { ScreenOffsets = [Offset(index, 0), Offset(index, 8), Offset(index + 1, 0), Offset(index + 1, 8)], SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
            }
            attempts.Add(packet.Attempt(primitive));
            allocated += 52;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static uint Gray(short value)
    {
        uint lane = unchecked((uint)(int)value);
        return lane | lane << 8 | lane << 16;
    }
}
