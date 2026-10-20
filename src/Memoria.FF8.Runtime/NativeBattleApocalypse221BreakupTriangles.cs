using System.Buffers.Binary;
using System.Collections.Immutable;
using M = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleApocalypse221BreakupProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, ushort Depth);
public sealed record NativeBattleApocalypse221BreakupTriangleProjection(NativeBattleWaterExecutionFrame Frame, ImmutableArray<uint> Screens, ushort Depth);
public sealed record NativeBattleApocalypse221BreakupTriangleServices(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleApocalypse221BreakupProjection> Project, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> SetLighting, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicGridValue> Shade, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> PrepareDetached, Func<NativeBattleWaterExecutionFrame, NativeBattleApocalypse221BreakupTriangleProjection> ProjectDetached, Func<int, uint> ReadColor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Intact and individually transformed textured triangle submission from the current face records.</summary>
public static class NativeBattleApocalypse221BreakupTriangles
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, uint state, uint vertices, NativeBattleApocalypse221BreakupTriangleServices services)
    {
        M m = new(frame);
        uint shape = m.Dword(state + 0xC);
        m.Frame = services.PrepareBasis(m.Frame);
        // B656A0 copies raw matrix padding together with its original known state.
        byte[] matrix = m.Frame.TemporaryFrame.Bytes.AsSpan().Slice(0xE0, 32).ToArray();
        bool[] matrixKnown = m.Frame.TemporaryFrame.Known.AsSpan().Slice(0xE0, 32).ToArray();
        m.Frame = m.Frame.WriteTemporary(0x40, matrix);
        ImmutableArray<bool>.Builder known = m.Frame.TemporaryFrame.Known.ToBuilder();
        for (int index = 0; index < matrixKnown.Length; index++)
        {
            known[0x40 + index] = matrixKnown[index];
        }
        m.Frame = m.Frame with { TemporaryFrame = m.Frame.TemporaryFrame with { Known = known.ToImmutable() } };
        m.Dword(m.Temporary + 0x78, m.Temporary + 0x40);
        uint work = m.Dword(m.Manager + 0x74);
        int vertexCount = unchecked((int)m.Dword(shape + 0x18));
        uint? previousScreen = null;
        uint? lastScreen = null;
        ushort? lastDepth = null;
        for (int index = 0; index < vertexCount; index++)
        {
            NativeBattleApocalypse221BreakupProjection projected = services.Project(m.Frame, unchecked(vertices + (uint)(8 * index)));
            m.Frame = projected.Frame;
            uint destination = unchecked(work + (uint)(8 * index));
            m.Dword(destination, projected.Screen);
            m.Word(destination + 4, projected.Depth);
            previousScreen = lastScreen;
            lastScreen = projected.Screen;
            lastDepth = projected.Depth;
        }
        m.Frame = services.SetLighting(m.Frame, m.Dword(m.Slot + 0xCC));
        uint face = unchecked(shape + m.Dword(shape + 8) + 4);
        uint output = m.Dword(m.Manager + 0x7C);
        work = m.Dword(m.Manager + 0x74);
        int count = m.Signed(state + 0xA);
        for (int index = 0; index < count; index++, face = unchecked(face + 20))
        {
            uint record = unchecked(state + 20u + (uint)(36 * index));
            byte[] data = new byte[32];
            data[3] = 7;
            PutWord(0x16, m.Word(face + 0x12) | 0x20);
            PutWord(0xE, m.Word(face + 0x10));
            PutWord(0xC, m.Word(face + 4));
            PutWord(0x14, m.Word(face + 6));
            PutWord(0x1C, m.Word(face + 8));
            uint ordering;
            if (m.Word(record) == 0)
            {
                uint[] projected = [unchecked(work + m.Word(face + 0xA)), unchecked(work + m.Word(face + 0xC)), unchecked(work + m.Word(face + 0xE))];
                PutDword(8, m.Dword(projected[0]));
                PutDword(0x10, m.Dword(projected[1]));
                PutDword(0x18, m.Dword(projected[2]));
                NativeBattleCinematicGridValue shaded = services.Shade(m.Frame, face);
                m.Frame = shaded.Frame;
                PutDword(4, (shaded.Value & 0xFFFFFF) | 0x26000000);
                uint depth = (uint)(m.Word(projected[0] + 4) + m.Word(projected[1] + 4) + m.Word(projected[2] + 4)) / 3;
                ordering = unchecked(m.Frame.ReadTemporaryDword(0x14C) + ((depth >> 2) & 0xFFFFFFFC));
            }
            else
            {
                int colorIndex = m.Signed(record + 2) % 24;
                PutDword(4, services.ReadColor(colorIndex));
                m.Frame = NativeBattleApocalypse221Breakup50.Integrate(m.Frame, record);
                m.Frame = services.PrepareDetached(m.Frame, record);
                for (uint vertex = 0; vertex < 3; vertex++)
                {
                    uint source = unchecked(vertices + m.Word(face + 0xA + vertex * 2));
                    uint destination = unchecked(m.Temporary + 0xC0 + vertex * 8);
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        m.Word(destination + axis * 2, m.Word(source + axis * 2) - m.Word(record + 4 + axis * 2));
                    }
                }
                NativeBattleApocalypse221BreakupTriangleProjection projected = services.ProjectDetached(m.Frame);
                m.Frame = projected.Frame;
                if (projected.Screens.IsDefault || projected.Screens.Length != 3)
                {
                    throw new InvalidDataException("Native Apocalypse detached triangle requires all three actual projected vertices.");
                }
                PutDword(8, projected.Screens[0]);
                PutDword(0x10, projected.Screens[1]);
                PutDword(0x18, projected.Screens[2]);
                previousScreen = projected.Screens[1];
                lastScreen = projected.Screens[2];
                lastDepth = projected.Depth;
                m.Word(m.Temporary + 0xC0, projected.Depth);
                ordering = unchecked(m.Frame.ReadTemporaryDword(0x14C) + (((uint)projected.Depth >> 2) & 0xFFFFFFFC));
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 32).Select(position => position >= 3 && position < 30 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, 8).Select(position => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position * 4))).ToImmutableArray();
            if (previousScreen is not { } sxy1 || lastScreen is not { } sxy2 || lastDepth is not { } sz3)
            {
                throw new NotSupportedException("Native Apocalypse triangle submission requires its actual retained SXY1/SXY2/SZ3 proxy payload.");
            }
            NativeBattleGeometryPrimitive primitive = new(0, words, [unchecked((int)sxy1), unchecked((int)sxy2), sz3, 0]) { PhysicalWriteMask = mask };
            m.Frame = services.Append(m.Frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 32));
            output = unchecked(output + 32);

            void PutDword(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
            void PutWord(int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), unchecked((ushort)value));
        }
        m.Dword(m.Manager + 0x7C, output);
        return m.Frame;
    }
}
