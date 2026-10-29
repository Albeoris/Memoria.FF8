using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicRadialMeshServices(Func<NativeBattleWaterExecutionFrame, int, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, int, NativeBattleCinematicGridValue> ResolvePalette, Func<NativeBattleWaterExecutionFrame, int, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> Project, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicGridValue> Shade, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append)
{
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
    public IReadOnlyDictionary<uint, System.Numerics.Vector3>? PilotWorld { get; init; }
}

/// <summary>Distance-controlled vertex colors and inverse intensity over one retained native mesh.</summary>
public static class NativeBattleCinematicRadialMesh
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, bool intensity, NativeBattleCinematicRadialMeshServices services)
    {
        uint payload = SlotDword(0xB8);
        uint state = SlotDword(0xBC);
        if (state == 0)
        {
            uint selected = services.ResolveShape(frame, Word(payload));
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 16);
            frame = WriteSlotDword(frame, 0xBC, state);
            StoreDword(state, selected);
            if (!intensity)
            {
                uint palette = 0;
                if (Word(payload + 4) != 65535)
                {
                    (frame, palette) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)Word(payload + 6) * 4));
                    StoreDword(state + 8, palette);
                    NativeBattleCinematicGridValue resolved = services.ResolvePalette(frame, Word(payload + 4));
                    frame = resolved.Frame;
                    uint source = resolved.Value;
                    int length = Word(payload + 6);
                    for (int color = 0; color < length; color++)
                    {
                        short packed = unchecked((short)Word(source + (uint)(color * 2)));
                        uint rgb = unchecked((byte)(packed << 3)) | (uint)((packed >> 2) & 248) << 8 | (uint)((packed >> 7) & 248) << 16;
                        StoreDword(palette + (uint)(color * 4), rgb);
                    }
                }
                else
                {
                    StoreDword(state + 8, 0);
                }
            }
        }
        payload = SlotDword(0xB8);
        state = SlotDword(0xBC);
        uint reference = services.ResolveSlot(frame, unchecked((short)Word(payload + (intensity ? 6u : 10u))));
        uint work = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        frame = frame.WriteTemporaryDword(0x80, work);
        uint shape = Dword(state);
        uint paletteSource = intensity ? 0 : Dword(state + 8);
        frame = frame.WriteTemporaryWord(0xC0, Word(reference + 0x94)).WriteTemporaryWord(0xC2, Word(reference + 0x96)).WriteTemporaryWord(0xC4, Word(reference + 0x98));
        int radius = unchecked((short)SlotWord(frame, 0x8C));
        int amplitude = unchecked((short)SlotWord(frame, 0x8E));
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)radius)).WriteTemporaryDword(0xF4, unchecked((uint)amplitude));
        int paletteOffset = 0;
        int paletteStep = 1;
        int paletteLength = 0;
        int maximum;
        if (intensity)
        {
            maximum = Word(payload + 4);
        }
        else
        {
            paletteOffset = unchecked((short)SlotWord(frame, 0x90));
            paletteStep = Math.Max(1, (int)unchecked((short)SlotWord(frame, 0x98)));
            paletteLength = Word(payload + 6);
            maximum = Word(payload + 8);
            frame = frame.WriteTemporaryDword(0xF8, unchecked((uint)paletteOffset)).WriteTemporaryDword(0xFC, unchecked((uint)paletteStep)).WriteTemporaryDword(0xE0, unchecked((uint)paletteLength));
        }
        frame = frame.WriteTemporaryDword(0xE4, unchecked((uint)maximum));
        uint vertices = unchecked(shape + Dword(shape + 0x14));
        int count = unchecked((int)Dword(shape + 0x18));
        uint cursor = work;
        for (int vertex = 0; vertex < count; vertex++)
        {
            uint source = unchecked(vertices + (uint)(vertex * 8));
            int dx = unchecked((short)Word(source)) - unchecked((short)frame.ReadTemporaryWord(0xC0));
            int dy = unchecked((short)Word(source + 2)) - unchecked((short)frame.ReadTemporaryWord(0xC2));
            int dz = unchecked((short)Word(source + 4)) - unchecked((short)frame.ReadTemporaryWord(0xC4));
            int planar = SquareRoot(unchecked(dx * dx + dy * dy));
            int distance = SquareRoot(unchecked(planar * planar + dz * dz));
            int inside = unchecked(radius - distance);
            uint color = intensity ? 256u : 0u;
            if (inside > 0)
            {
                int strength = Math.Max(0, Math.Min(unchecked(amplitude * inside) / radius, maximum));
                if (intensity)
                {
                    color = unchecked((uint)(256 - strength));
                }
                else
                {
                    uint material;
                    if (paletteSource == 0)
                    {
                        material = SlotDword(0xCC) & 0xFFFFFF;
                    }
                    else
                    {
                        int selected = Math.Max(0, unchecked(distance / paletteStep + paletteOffset));
                        material = Dword(unchecked(paletteSource + (uint)(selected % paletteLength * 4)));
                    }
                    NativeBattleCinematicGridValue shaded = services.Shade(frame, material, unchecked((short)strength));
                    frame = shaded.Frame;
                    color = shaded.Value;
                }
            }
            StoreDword(cursor, color);
            cursor = unchecked(cursor + 4);
        }
        frame = frame.WriteTemporaryDword(0x84, cursor);
        frame = services.PrepareBasis(frame, Word(payload + 2) << 4);
        for (int vertex = 0; vertex < count; vertex++)
        {
            frame = services.Project(frame, unchecked(vertices + (uint)(vertex * 8)), cursor);
            cursor = unchecked(cursor + 8);
        }
        uint group = unchecked(shape + Dword(shape + 8));
        while (true)
        {
            short type = unchecked((short)Word(group));
            if (intensity ? type is not (9 or 19) : type is not (8 or 9 or 18 or 19))
            {
                return frame;
            }
            var result = NativeBattleCinematicVertexColorPackets.Emit(frame, group, frame.ReadTemporaryDword(0x84), frame.ReadTemporaryDword(0x80), intensity, services.Shade, services.Append, services.PilotWorld, services.PilotIdentity);
            frame = result.Frame;
            group = result.Next;
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreDword(uint address, uint value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        }
    }

    private static int SquareRoot(int value) => unchecked((int)(long)Math.Sqrt(value));
}
