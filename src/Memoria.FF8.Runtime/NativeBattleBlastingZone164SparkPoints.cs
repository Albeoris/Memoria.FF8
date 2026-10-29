using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBlastingZone164Controller
{
    public static NativeBattleBlastingZone164ChildStep Advance5F4CC0(NativeBattleBlastingZone164Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleBlastingZone164Inputs inputs, NativeBattleCameraProjection projection)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        if (!paused && Word(task, 12) <= 28)
        {
            for (int index = 0; index < 5; index++)
            {
                uint row = 0x23C6978;
                while (row < 0x23C72D8 && ReadDword(frame.Staging, row) != 0)
                {
                    row += 24;
                }
                if (row == 0x23C72D8)
                {
                    break;
                }
                NativeBattleSquallFinisherVisualIdentity.Allocated(164, task, row);
                frame = frame with { Staging = WriteWord(WriteDword(frame.Staging, row, 1), row + 4, 0) };
                uint source = 0x23C9DC8u + (uint)(Rand() % 105) * 8;
                frame = frame with { Staging = frame.Staging.Write(row + 8, frame.Staging.Read(source, 8)) };
                int x = Rand() % 4096 - 2048;
                int y = Rand() % 4096 - 2048;
                int z = Rand() % 4096 - 2048;
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
                int speed = Rand() % 35 + 12;
                SetWord(row + 16, (speed * direction.X) >> 12);
                SetWord(row + 18, (speed * direction.Y) >> 12);
                SetWord(row + 20, (speed * direction.Z) >> 12);
            }
        }
        NativeBattleActionMatrix matrix = ReadMatrix(frame.Staging, 0x23C9DA8);
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder packets = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        for (uint row = 0x23C6978; row < 0x23C72D8; row += 24)
        {
            if ((ReadDword(frame.Staging, row) & 1) == 0)
            {
                continue;
            }
            NativeBattleVector point = new(ReadWord(frame.Staging, row + 8), ReadWord(frame.Staging, row + 10), ReadWord(frame.Staging, row + 12));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(matrix, point, projection, 0, 0);
            short age = ReadWord(frame.Staging, row + 4);
            if (age is < 0 or >= 12)
            {
                throw new InvalidDataException("Native Blasting Zone spark age leaves its original color table.");
            }
            uint color = unchecked((uint)ReadDword(frame.Staging, 0xD99C1Cu + (uint)age * 4));
            if (!paused)
            {
                age++;
                SetWord(row + 4, age);
                if (age < 12)
                {
                    SetWord(row + 8, point.X + ReadWord(frame.Staging, row + 16));
                    SetWord(row + 10, point.Y + ReadWord(frame.Staging, row + 18));
                    SetWord(row + 12, point.Z + ReadWord(frame.Staging, row + 20));
                }
                else
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, row, 0) };
                }
            }
            if ((projected.Flags & 0x60000) == 0)
            {
                System.Numerics.Vector3 world = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                NativeBattlePilotVisualGeometry geometry = new(NativeBattleSquallFinisherVisualIdentity.Particle(164, task, row, 0), world, world, world, world, 4) { ScreenOffsets = [new(0, 0), new(8, 0), new(0, 8), new(8, 8)], UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero };
                NativeBattleGeometryPrimitive primitive = new(17 + (projected.Depth >> 4), [0x02000000, color, projected.Screen], []) { PilotGeometry = geometry };
                packets.Add(new(primitive, packets.Count * 12, 0));
            }
        }
        frame = inputs.Execute(frame, new NativeBattleBlastingZone164Tiles(new(packets.ToImmutable(), packets.Count * 12)));
        if (paused)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        bool released = Word(task, 12) >= 36;
        if (released)
        {
            for (uint row = 0x23C6978; row < 0x23C72D8; row += 24)
            {
                if ((ReadDword(frame.Staging, row) & 1) != 0)
                {
                    frame = frame with { Staging = WriteDword(frame.Staging, row, 0) };
                }
            }
        }
        return new(frame, pool.WriteSlot(slot, task), released ? 2 : 0);

        void SetWord(uint address, int value)
        {
            frame = frame with { Staging = WriteWord(frame.Staging, address, unchecked((ushort)value)) };
        }
        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
