using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89StarDraw(NativeBattleRinoa89Frame Frame, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleRinoa89StarDrawing
{
    public static NativeBattleRinoa89StarDraw Draw6FCD50(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, int centerX, int centerY, int centerZ, int depthBias, int radiusLimit, bool captureVisual = false)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native6FCD50 requires its retained1024-star source.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 100);
        uint temporary = allocation.Address;
        NativeBattleFileStaging staging = Rotation(allocation.Staging, temporary, view, true);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(view, centerX, centerY, centerZ);
        x = unchecked(x + view.X);
        y = unchecked(y + view.Y);
        z = unchecked(z + view.Z);
        staging = Dword(Dword(Dword(staging, temporary + 20, unchecked((uint)x)), temporary + 24, unchecked((uint)y)), temporary + 28, unchecked((uint)z));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        for (int group = 0; group < 8; group++)
        {
            byte[] angles = staging.Read(checked(0x14AF49Cu + (uint)group * 16), 4);
            NativeBattleActionMatrix pitch = NativeBattlePandemona291Restoration.Rotation701220(BinaryPrimitives.ReadInt16LittleEndian(angles));
            NativeBattleActionMatrix yaw = NativeBattlePandemona291Restoration.Rotation701270(BinaryPrimitives.ReadInt16LittleEndian(angles.AsSpan(2)));
            staging = Rotation(Rotation(staging, temporary + 64, pitch, false), temporary + 32, yaw, false);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(yaw, pitch);
            staging = Rotation(staging, temporary + 64, local, true);
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(view, local) with { X = x, Y = y, Z = z };
            staging = Rotation(staging, temporary + 64, matrix, true);
            for (int index = 0; index < 128; index++)
            {
                byte[] star = arena.Read(31748 + (group * 128 + index) * 16, 16);
                if (BinaryPrimitives.ReadInt16LittleEndian(star.AsSpan(6)) >= radiusLimit)
                {
                    continue;
                }
                NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(star), BinaryPrimitives.ReadInt16LittleEndian(star.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(star.AsSpan(4)));
                NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
                int quarterDepth = projected.Depth >> 2;
                staging = Dword(staging, temporary + 96, unchecked((uint)quarterDepth));
                byte[] bytes = new byte[12];
                NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[12];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), projected.Screen);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, 8, 4);
                NativeBattleGeometryPrimitive? primitive = null;
                if (quarterDepth > 0)
                {
                    uint first = BinaryPrimitives.ReadUInt32LittleEndian(star.AsSpan(8));
                    uint second = BinaryPrimitives.ReadUInt32LittleEndian(star.AsSpan(12));
                    uint color = BlendColor6FCD50(first, second, star[11]) | 0x68000000;
                    int bucket = Math.Max(unchecked((quarterDepth >> 2) + depthBias), 0);
                    staging = Dword(staging, temporary + 96, unchecked((uint)bucket));
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x02000000);
                    BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), color);
                    Array.Fill(mask, NativeBattleGfPacketByteState.Known, 0, 8);
                    ImmutableArray<uint> words = [0x02000000, color, projected.Screen];
                    primitive = new(checked(17 + bucket), words, [0, 0, 0, 0]) { PhysicalWriteMask = mask.ToImmutableArray() };
                    if (captureVisual)
                    {
                        System.Numerics.Vector3 world = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection);
                        primitive = primitive with { PilotGeometry = new(NativeBattleRinoa89Visuals.Row(arena, 31748, group * 128 + index), world, world, world, world, 4) { ScreenOffsets = [new(0, 0), new(8, 0), new(0, 8), new(8, 8)] } };
                    }
                }
                attempts.Add(new(cursor, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                if (primitive is not null)
                {
                    cursor += 12;
                }
            }
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 100);
        return new(frame with { Staging = staging }, new(attempts.ToImmutable(), cursor));
    }

    public static uint BlendColor6FCD50(uint first, uint second, byte alpha)
    {
        if (alpha == 0)
        {
            return first & 0xFFFFFF;
        }
        int weight = alpha * 16;
        NativeBattleVector a = new((byte)first, (byte)(first >> 8), (byte)(first >> 16));
        NativeBattleVector b = new((byte)second, (byte)(second >> 8), (byte)(second >> 16));
        NativeBattleVector blended = NativeBattleQuezacotlRibbonMath.Blend(a, b, weight, 4096 - weight);
        return unchecked((byte)blended.X) | (uint)unchecked((byte)blended.Y) << 8 | (uint)unchecked((byte)blended.Z) << 16;
    }

    private static NativeBattleFileStaging Rotation(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix, bool unknownPadding)
    {
        byte[] bytes = new byte[20];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), matrix.Rotation[index]);
        }
        staging = staging.Write(address, bytes);
        return unknownPadding ? staging.MarkUnknown(address + 18, 2) : staging;
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));
}
