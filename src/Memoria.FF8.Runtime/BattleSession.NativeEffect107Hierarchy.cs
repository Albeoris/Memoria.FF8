using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGfAnimationWorkspace AdvanceNativeEffect107Hierarchy(ref NativeBattleGraphicsState graphics, ref NativeBattleOriginalEffectState effect, NativeBattleEffectTaskPool[] pools, ref NativeCrtRandomState random, NativeBattleEffect107Resources resources, NativeBattleGfAnimationWorkspace workspace, NativeBattleGfAnimationNode node, NativeBattleVector anchor, uint workspacePointer)
    {
        NativeCrtRandomState currentRandom = random;
        NativeBattleVector scale = node.Vector24;
        bool skipped = scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0;
        if (node.NodeIndex == 0)
        {
            byte[] data = workspace.Data.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(18), skipped ? (short)0 : scale.X);
            workspace = new(data.ToImmutableArray());
            if (!skipped)
            {
                if (workspace.Frame == 14)
                {
                    for (int index = 0; index < 32; index++)
                    {
                        if (Allocate(3, 0x6D2990, out int slot))
                        {
                            NativeBattleModulePoolTask child = pools[3].Slots[slot].ClearPayload(20);
                            child = child.WriteDword(16, workspacePointer).WriteWord(12, (ushort)(Roll() * 9 >> 15)).WriteWord(20, Roll()).WriteWord(24, Roll()).WriteWord(26, Roll()).WriteWord(28, Roll()).WriteWord(22, (ushort)((Roll() & 1023) + 1500));
                            Store(3, slot, child);
                        }
                    }
                }
                if ((uint)(workspace.Frame - 30) < 37)
                {
                    int threshold = workspace.Frame - 30 <= 18 ? 24576 : 12288;
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        if (Roll() < threshold && Allocate(4, 0x6D2910, out int slot))
                        {
                            NativeBattleModulePoolTask child = pools[4].Slots[slot].ClearPayload(16);
                            uint address = (Roll() & 1) == 0 ? 0x12EC414u : 0x12EC574u;
                            int first = unchecked((short)Roll());
                            int second = unchecked((short)Roll());
                            int sine = NativeBattleCameraMath.Sine(first);
                            NativeBattleVector unit = new(unchecked((short)(NativeBattleCameraMath.Cosine(second) * sine >> 12)), unchecked((short)-NativeBattleCameraMath.Cosine(first)), unchecked((short)(-NativeBattleCameraMath.Sine(second) * sine >> 12)));
                            int quarter = scale.X >> 2;
                            int distance = (Roll() * quarter >> 17) + quarter;
                            NativeBattleVector point = new(unchecked((short)((unit.X * distance >> 12) + anchor.X)), unchecked((short)((unit.Y * distance >> 12) + anchor.Y)), unchecked((short)((unit.Z * distance >> 12) + anchor.Z)));
                            child = child.WriteDword(16, address).WriteWord(14, 13).WriteWord(26, 4096).WriteWord(20, unchecked((ushort)point.X)).WriteWord(22, unchecked((ushort)point.Y)).WriteWord(24, unchecked((ushort)point.Z));
                            Store(4, slot, child);
                        }
                    }
                }
            }
        }
        if (skipped)
        {
            random = currentRandom;
            return workspace;
        }
        ImmutableArray<byte> source = resources.Hierarchy.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(geometry + 4));
        if (count < 0 || count > 4096)
        {
            throw new InvalidDataException("Native6D1F50 requires its bounded original geometry vertex count.");
        }
        int firstShape = node.Word40;
        int secondShape = node.Word42;
        int ratio = node.Scalar38;
        ImmutableArray<byte> vertices;
        if (firstShape == secondShape || ratio is 0 or 4096)
        {
            int shape = firstShape == secondShape || ratio == 0 ? firstShape : secondShape;
            vertices = source.AsSpan().Slice(geometry + 12 + shape * count * 8, count * 8).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = NativeEffectMemory!.Arena!;
            ImmutableArray<NativeBattleVector> morphed = NativeBattleGfMorphGeometry.MorphVertices701390(source, geometry, firstShape, secondShape, ratio);
            for (int index = 0; index < morphed.Length; index++)
            {
                byte[] bytes = new byte[6];
                NativeBattleVector vertex = morphed[index];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, vertex.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), vertex.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), vertex.Z);
                arena = arena.Write(0x1DC8 + index * 8, bytes);
            }
            NativeEffectMemory = NativeEffectMemory with { Arena = arena };
            vertices = arena.Read(0x1DC8, count * 8).ToImmutableArray();
        }
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(0x12F9998 + (uint)node.NodeIndex * 4, 4).AsSpan());
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(node.Vector16);
        matrix = matrix with { X = unchecked(node.Vector8.X + anchor.X), Y = unchecked(node.Vector8.Y + anchor.Y), Z = unchecked(node.Vector8.Z + anchor.Z) };
        if ((flags & 0x10000000) != 0)
        {
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
            matrix = matrix with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
        }
        else
        {
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
        }
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = matrix with { Rotation = matrix.Rotation.Select((value, index) => unchecked((short)(value * (index % 3 == 0 ? scale.X : index % 3 == 1 ? scale.Y : scale.Z) >> 12))).ToImmutableArray() };
        }
        uint renderFlags = flags & 0xDFFFu | 0x2000u;
        int cue = node.Scalar36;
        uint color = 0;
        if (cue != 0)
        {
            renderFlags |= 0xC0;
            color = node.Color32;
        }
        NativeBattleGfGeometryBatch batch;
        if ((flags & 0x20000000) != 0)
        {
            cue = node.NodeIndex == 9 ? -128 : node.NodeIndex == 5 ? 128 : 0;
            batch = NativeBattleEffect107Geometry.EmitBatch6D23D0(source, geometry, vertices, matrix, projection, new(renderFlags, color, cue, null, null, 0, 17, 2));
        }
        else
        {
            int depthBias = (flags & 0x40000000) != 0 ? -512 : 0;
            batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source, geometry, vertices, matrix, projection, new(renderFlags, color, cue, null, null, depthBias, 17, 2), definition.NativeGraphics!.Rules.SummonColors);
        }
        NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(NativeEffectMemory!, effect.DrawArenaOffset!.Value, frame.Pending, batch);
        NativeEffectMemory = written.Memory;
        effect = effect with { DrawArenaOffset = written.Cursor };
        graphics = graphics with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        random = currentRandom;
        return workspace;

        ushort Roll()
        {
            currentRandom = currentRandom.Draw(out ushort value);
            return value;
        }
        bool Allocate(int index, uint callback, out int slot)
        {
            if (pools[index].Slots.All(task => (task.Flags & 1) != 0))
            {
                slot = -1;
                return false;
            }
            (pools[index], slot) = pools[index].Append(callback);
            return true;
        }
        void Store(int index, int slot, NativeBattleModulePoolTask task) => pools[index] = pools[index].WriteSlot(slot, task);
    }
}
