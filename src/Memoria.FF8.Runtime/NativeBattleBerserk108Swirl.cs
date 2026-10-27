using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBerserk108SwirlStep(NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets, int Result);

public static class NativeBattleBerserk108Swirl
{
    public static NativeBattleBerserk108SwirlStep Advance(NativeBattleModulePoolTask task, NativeBattleVector anchor, NativeBattleCameraProjection projection, ImmutableArray<byte> windows)
    {
        if (task.PayloadLength != 0x414 || windows.Length != 16)
        {
            throw new InvalidDataException("Native Berserk requires its original swirl bank and texture rectangles.");
        }
        int counter = R(12);
        int count = unchecked((int)U(24));
        if ((uint)count > 32)
        {
            throw new InvalidDataException("Native Berserk trail leaves its32 physical entries.");
        }
        NativeBattleActionMatrix view = projection.View!;
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
        x = unchecked(x + view.X);
        y = unchecked(y + view.Y);
        z = unchecked(z + view.Z);
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(-x, -y, z).Matrix with { X = x, Y = y, Z = z };
        if (counter <= 6)
        {
            for (int index = 0; index < 4; index++)
            {
                int phase = 4096 - ((counter * 4 + index) << 12) / 24;
                if (count < 32)
                {
                    task.WriteRetainedBytes(0x40, task.ReadRawPayloadSpan().Slice(0x20 - 12, count * 32));
                    count++;
                }
                W(0x20, R(16));
                W(0x22, unchecked(z * phase) >> 12);
                W(0x24, (NativeBattleCameraMath.Sine(phase >> 1) * R(22) >> 12) + phase);
                W(0x26, phase);
                W(0x38, 16);
                W(0x3A, 256);
                W(0x3C, (7 - ((counter * 4 + index) & 7)) << 4);
                W(16, R(16) + 32);
            }
        }
        for (int index = 0; index < count; index++)
        {
            int entry = 0x20 + index * 32;
            if (R(entry + 24) >= 16)
            {
                W(entry + 26, R(entry + 26) - 40);
                W(entry + 24, Math.Clamp(R(entry + 24) + (R(entry + 26) >> 3), 16, 255));
            }
        }
        if (count >= 2 && R(0x20 + (count - 1) * 32 + 24) <= 16 && R(0x20 + (count - 2) * 32 + 24) <= 16)
        {
            count--;
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        if (count >= 2)
        {
            for (int index = 0; index < count; index++)
            {
                int entry = 0x20 + index * 32;
                Project(entry, -128, 8, 12);
                Project(entry, 128, 16, 20);
                int oldPhase = R(entry + 6);
                W(entry + 6, oldPhase + 512);
                W(entry, R(entry) + (NativeBattleCameraMath.Sine(oldPhase) >> 6));
            }
            for (int index = 0; index < count - 1; index++)
            {
                int a = 0x20 + index * 32;
                int b = a + 32;
                int[] depth = [R(a + 12), R(a + 20), R(b + 12), R(b + 20)];
                if (depth.Any(value => value <= 0))
                {
                    continue;
                }
                uint a0 = U(a + 8);
                uint a1 = U(a + 16);
                uint b0 = U(b + 8);
                uint b1 = U(b + 16);
                bool swap = (NativeBattleModulePackets.Nclip(a0, a1, b0) > 0) != (NativeBattleModulePackets.Nclip(a1, b1, b0) > 0);
                uint u = unchecked((uint)((counter << 5) + R(a + 28)));
                uint end = unchecked(u + 15);
                if (unchecked((int)(u | end)) > 255)
                {
                    u = unchecked(u - 128);
                    end = unchecked(end - 128);
                }
                uint colorA = unchecked((uint)R(a + 24) * 0x10101);
                uint colorB = unchecked((uint)R(b + 24) * 0x10101);
                ImmutableArray<uint> words = [0x0C000000, 0x3E000000 | colorA, a0, u | 0x3D94C000, colorA, a1, u | 0xB9FF00, colorB, swap ? b1 : b0, end | (swap ? 0xFF00u : 0xC000u), colorB, swap ? b0 : b1, end | (swap ? 0xC000u : 0xFF00u)];
                NativeBattleGfGeometryAttempt stores = NativeBattleBerserk108Geometry.Packet(words, allocated, 17 + (depth.Sum() >> 4));
                NativeBattleGfGeometryBatch bracket = NativeBattleEffectTextureWindows.Bracket(stores, stores.Primitive!, Rectangle(0), Rectangle(8));
                attempts.AddRange(bracket.Attempts);
                allocated += bracket.AllocationBytes;
            }
        }
        D(24, (uint)count);
        W(12, counter + 1);
        return new(task, new(attempts.ToImmutable(), allocated), counter + 1 > 8 && count < 2 ? 2 : 0);

        short R(int offset) => BinaryPrimitives.ReadInt16LittleEndian(task.ReadRawPayloadSpan().Slice(offset - 12));
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(task.ReadRawPayloadSpan().Slice(offset - 12));
        void W(int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
        void D(int offset, uint value) => task.WriteDword(offset, value);
        NativeBattleTextureWindow Rectangle(int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(windows.AsSpan().Slice(offset)), BinaryPrimitives.ReadInt16LittleEndian(windows.AsSpan().Slice(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(windows.AsSpan().Slice(offset + 4)), BinaryPrimitives.ReadInt16LittleEndian(windows.AsSpan().Slice(offset + 6)));
        void Project(int entry, int angleDelta, int screenOffset, int depthOffset)
        {
            int angle = R(entry) + angleDelta;
            int radius = R(entry + 4);
            int height = R(entry + 2);
            NativeBattleVector point = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) * radius >> 14)), unchecked((short)(NativeBattleCameraMath.Sine(angle) * radius >> 14)), unchecked((short)((height >> 2) - height)));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(basis, point, projection);
            D(entry + screenOffset, projected.Screen);
            D(entry + depthOffset, projected.Depth);
        }
    }
}
