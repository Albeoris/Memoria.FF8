using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWaterActorDraw
{
    public static NativeBattleWaterActorShadowResult FloorShadowA9C670(NativeBattleWaterExecutionFrame frame, NativeBattleGraphicsModel actor, NativeBattleActionMatrix basis, NativeBattleVector position, NativeBattleWaterActorPoseServices services, NativeBattleCameraProjection projection, short dqa, int dqb, uint orderingTablePointer, Func<uint, int> resolveActualOtBucket)
    {
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x20) };
        NativeBattleWaterActorPoseResult local = services.ReadLocal508C90(frame, actor);
        frame = local.Frame;
        if (local.Skeleton.IsDefaultOrEmpty || local.Skeleton[0] == 0 || local.LocalBones.IsDefault || local.LocalBones.Length != local.Skeleton[0] || local.Skeleton.Length < 0x10 + local.Skeleton[0] * 0x30)
        {
            throw new InvalidDataException("Native actor shadow requires its complete current local rig and own bone lengths.");
        }
        ImmutableArray<NativeBattleActionMatrix> world = NativeBattlePoseMatrices.ComposeStageCamera(local.LocalBones, basis, 0);
        frame = services.PublishBones5095B0(frame, actor, world);
        int minX = unchecked(world[0].X - 500);
        int maxX = unchecked(world[0].X + 500);
        int minZ = unchecked(world[0].Z - 500);
        int maxZ = unchecked(world[0].Z + 500);
        frame = frame.WriteTemporaryDword(0xD0, 0);
        for (int index = 1; index < world.Length; index++)
        {
            short length = BinaryPrimitives.ReadInt16LittleEndian(local.Skeleton.AsSpan().Slice(0x10 + index * 0x30 + 2));
            frame = frame.WriteTemporaryWord(0xD4, unchecked((ushort)length));
            NativeBattleFullMacVertex full = NativeBattleGfMatrices.TransformMac460840(world[index], new(0, 0, length));
            NativeBattleVector point = Saturate(full);
            frame = TemporaryVector(frame, 0xD8, point).WriteTemporaryDword(0xFC, full.Flags);
            if (point.X < minX)
            {
                minX = point.X;
            }
            else if (point.X > maxX)
            {
                maxX = point.X;
            }
            if (point.Z < minZ)
            {
                minZ = point.Z;
            }
            else if (point.Z > maxZ)
            {
                maxZ = point.Z;
            }
        }
        int centerX = unchecked(minX + maxX) / 2;
        int centerZ = unchecked(minZ + maxZ) / 2;
        int extentX = unchecked(maxX - centerX);
        int extentZ = unchecked(maxZ - centerZ);
        NativeBattleVector center = new(unchecked((short)(position.X + centerX)), 0, unchecked((short)(position.Z + centerZ)));
        frame = TemporaryVector(frame, 0xD0, center);
        NativeBattleActionMatrix camera = ReadMatrix(frame, 0x27979E8);
        NativeBattleFullMacVertex cameraCenter = NativeBattleGfMatrices.TransformMac460840(camera, center);
        NativeBattleVector translated = Saturate(cameraCenter);
        frame = TemporaryVector(frame, 0xD8, translated).WriteTemporaryDword(0xFC, cameraCenter.Flags);
        NativeBattleActionMatrix projectedMatrix = camera with { X = translated.X, Y = translated.Y, Z = translated.Z };
        int sine = NativeBattleCameraMath.Sine(0x200);
        int sx = unchecked(sine * extentX) >> 12;
        int sz = unchecked(sine * extentZ) >> 12;
        (int X, int Z)[] points = [(0, extentZ), (sx, sz), (extentX, 0), (sx, -sz), (0, -extentZ), (-sx, -sz), (-extentX, 0), (-sx, sz), (0, 0)];
        uint[] screens = new uint[9];
        for (int index = 0; index < points.Length; index++)
        {
            NativeBattleVector point = new(unchecked((short)points[index].X), 0, unchecked((short)points[index].Z));
            int offset = 0xB0 + index * 8;
            frame = TemporaryVector(frame, offset, point);
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(projectedMatrix, point, projection, dqa, dqb);
            screens[index] = projected.Screen;
            frame = frame.WriteTemporaryDword(offset, projected.Screen).WriteTemporaryDword(0xFC, projected.Flags);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (NativeBattleModulePackets.Nclip(screens[0], screens[2], screens[4]) > 0)
        {
            uint color = actor.Color44 ?? throw new NotSupportedException("Native actor shadow requires its current shadow-color DWORD.");
            int bucket = resolveActualOtBucket(unchecked(orderingTablePointer + 0x4064));
            for (int index = 0; index < 8; index++)
            {
                uint[] words = [0x08000000, 0xE1000040, 0, color, screens[8], 0, screens[index], 0, screens[(index + 1) & 7]];
                byte[] bytes = new byte[36];
                for (int word = 0; word < words.Length; word++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), words[word]);
                }
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToArray();
                Array.Fill(mask, NativeBattleGfPacketByteState.Untouched, 0, 3);
                attempts.Add(new(allocated, bytes.ToImmutableArray(), mask.ToImmutableArray(), new(bucket, words.ToImmutableArray(), [0, 0, 0, 0])));
                allocated += 36;
            }
        }
        if (allocated != 0)
        {
            uint manager = CoreDword(frame, 0x27973EC);
            uint cursor = CoreDword(frame, checked(manager + 0x7C));
            frame = WriteCoreDword(frame, checked(manager + 0x7C), unchecked(cursor + (uint)allocated));
        }
        NativeBattleWaterActorPoseResult restored = services.ReadLocal508C90(frame, actor);
        return new(restored.Frame, actor, new(attempts.ToImmutable(), allocated));
    }
}
