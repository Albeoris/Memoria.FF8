using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRoughDivide163Controller
{
    public static NativeBattleRoughDivide163ChildStep Advance5F7390(NativeBattleRoughDivide163Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRoughDivide163Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory registers, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        if (counter < 0)
        {
            throw new NotSupportedException("Native5F7390 requires its original nonnegative history counter.");
        }
        NativeBattleFileStaging staging = frame.Staging;
        int count = Math.Min(counter + 1, 11);
        if (counter < 11)
        {
            uint destination = checked(0x23CE318u + (uint)counter * 56);
            staging = Copy(Copy(staging, 0x23CD318, destination, 8), 0x23CD320, destination + 8, 16);
        }
        if (count >= 2)
        {
            for (int index = 0; index < count; index++)
            {
                uint current = checked(0x23CE318u + (uint)index * 56);
                uint other = index == 0 ? current + 56 : current - 56;
                int dx = index == 0 ? ReadWord(staging, other) - ReadWord(staging, current) : ReadWord(staging, current) - ReadWord(staging, other);
                int dz = index == 0 ? ReadWord(staging, other + 4) - ReadWord(staging, current + 4) : ReadWord(staging, current + 4) - ReadWord(staging, other + 4);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                NativeBattleModuleAxisAngle angle = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
                NativeBattleActionMatrix basis = new(NativeBattleModuleAlignment.Rotation5714F0(angle.Angle, angle.Axis), 0, 0, 0, 4096);
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(Word(task, 28), 0, 0));
                uint destination = checked(0x23CE168u + (uint)index * 24);
                staging = Copy(staging, current, destination, 8);
                staging = WriteWord(staging, destination + 8, unchecked((ushort)(ReadWord(staging, current) + offset.X)));
                staging = WriteWord(staging, destination + 10, 0);
                staging = WriteWord(staging, destination + 12, unchecked((ushort)(ReadWord(staging, current + 4) + offset.Z)));
                staging = WriteWord(staging, destination + 16, unchecked((ushort)(ReadWord(staging, current) - offset.X)));
                staging = WriteWord(staging, destination + 18, 0);
                staging = WriteWord(staging, destination + 20, unchecked((ushort)(ReadWord(staging, current + 4) - offset.Z)));
            }
            frame = frame with { Staging = staging };
            (NativeBattleGfGeometryBatch batch, int cursor) = DrawEcho(staging, count, counter, view, projection, registers, colors, NativeBattlePilotVisualLifetimes.Identity(163, task));
            frame = inputs.Execute(frame, new NativeBattleRoughDivide163Trail(batch, cursor));
        }
        else
        {
            frame = frame with { Staging = staging };
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (counter < 20)
        {
            task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + 5)));
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool.WriteSlot(slot, task), Word(task, 12) >= 40 ? 2 : 0);
    }

    private static (NativeBattleGfGeometryBatch Batch, int Cursor) DrawEcho(NativeBattleFileStaging staging, int count, short age, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory registers, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity identity)
    {
        uint bright = 0x3A202080;
        uint middle = 0x3A04040E;
        const uint black = 0x3A000000;
        if (age >= 32)
        {
            int fade = (age - 32) << 9;
            bright = colors.Interpolate(bright, fade, 0) | 0x3A000000;
            middle = colors.Interpolate(middle, fade, 0) | 0x3A000000;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        Dictionary<uint, System.Numerics.Vector3> world = [];
        for (int segment = 0; segment < count - 1; segment++)
        {
            uint current = checked(0x23CE168u + (uint)segment * 24);
            uint next = current + 24;
            NativeBattleModuleProjectedVertex nextCenter = Project(next);
            NativeBattleModuleProjectedVertex currentLeft = Project(current + 8);
            NativeBattleModuleProjectedVertex currentRight = Project(current + 16);
            if (((nextCenter.Flags | currentLeft.Flags | currentRight.Flags) & 0x60000) != 0)
            {
                attempts.Add(Header(allocation));
                attempts.Add(Header(allocation + 36));
                continue;
            }
            short zsf3 = registers.Zsf3 ?? throw new NotSupportedException("Native5F7390 requires its actual ZSF3 register.");
            uint depth = unchecked((uint)(nextCenter.Depth + currentLeft.Depth + currentRight.Depth) * (uint)(int)zsf3) >> 12;
            int bucket = unchecked(17 + (unchecked((int)depth) >> 2));
            NativeBattleModuleProjectedVertex currentCenter = Project(current);
            NativeBattleModuleProjectedVertex nextLeft = Project(next + 8);
            NativeBattleModuleProjectedVertex nextRight = Project(next + 16);
            uint[] a;
            uint[] b;
            if (segment == 0)
            {
                a = [middle, bright, black, middle];
                b = [bright, middle, middle, black];
            }
            else if (segment == count - 2)
            {
                a = [black, middle, middle, bright];
                b = [middle, black, bright, middle];
            }
            else
            {
                a = [middle, bright, middle, bright];
                b = [bright, middle, bright, middle];
            }
            NativeBattlePilotVisualGeometry first = new(identity with { Primitive = segment * 2 }, world[next + 8], world[next], world[current + 8], world[current], 4);
            NativeBattlePilotVisualGeometry second = new(identity with { Primitive = segment * 2 + 1 }, world[next], world[next + 16], world[current], world[current + 16], 4);
            attempts.Add(EchoPacket(allocation, bucket, [nextLeft, nextCenter, currentLeft, currentCenter], a, first));
            attempts.Add(EchoPacket(allocation + 36, bucket, [nextCenter, nextRight, currentCenter, currentRight], b, second));
            allocation += 72;
        }
        return (new(attempts.ToImmutable(), allocation), checked(allocation + 36));

        NativeBattleModuleProjectedVertex Project(uint address)
        {
            NativeBattleVector point = new(ReadWord(staging, address), ReadWord(staging, address + 2), ReadWord(staging, address + 4));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, point, projection);
            world[address] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(view, point), projection);
            return projected;
        }
    }

    private static NativeBattleGfGeometryAttempt Header(int offset)
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x08000000);
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 36).Select(index => index < 4 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
        return new(offset, bytes.ToImmutableArray(), mask, null);
    }

    private static NativeBattleGfGeometryAttempt EchoPacket(int offset, int bucket, NativeBattleModuleProjectedVertex[] points, uint[] colors, NativeBattlePilotVisualGeometry geometry)
    {
        uint[] words = [0x08000000, colors[0], points[0].Screen, colors[1], points[1].Screen, colors[2], points[2].Screen, colors[3], points[3].Screen];
        byte[] bytes = new byte[36];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), points.Select(point => (int)point.Depth).ToImmutableArray()) { PhysicalWriteMask = mask, PilotGeometry = geometry };
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }
}
