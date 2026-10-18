using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFullscreenOverlayProgram(uint Address, ImmutableArray<uint> Commands);
public sealed record NativeBattleFullscreenOverlayState([property: JsonRequired] NativeBattleEffectArena Packets)
{
    public const int Length = 108;
    public static NativeBattleFullscreenOverlayState FromColdProcess() => new(new NativeBattleEffectArena([]));
    public static NativeBattleFullscreenOverlayState Unknown() => new(new NativeBattleEffectArena([]) { UnknownRegions = [new(0, Length)] });
    public void Validate()
    {
        Packets.Validate();
        if (Packets.Regions.Any(region => region.Offset > Length - region.Data.Length) || Packets.UnknownRegions.Any(region => region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Native fullscreen static packets leave their108-byte physical owner.");
        }
    }
    public byte[] Read(int offset, int count)
    {
        Validate();
        if (offset < 0 || count < 0 || offset > Length - count)
        {
            throw new InvalidDataException("Native fullscreen packet read leaves its actual static owner.");
        }
        return Packets.Read(offset, count);
    }
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? FullscreenOverlayPause2795914 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFullscreenOverlayState? FullscreenOverlay { get; init; }
}
public sealed record NativeBattleFullscreenOverlayFrame(NativeBattleEffectMemory Memory, ImmutableArray<NativeBattleEffectTaskPool> Pools, NativeBattleLogicalOrdering Ordering);
public sealed record NativeBattleFullscreenOverlayStep(NativeBattleFullscreenOverlayFrame Frame, int Result);

/// <summary>8DDEC0's bounded program and five fixed physical packets, independent of effect/Common allocation cursors.</summary>
public static class NativeBattleFullscreenOverlay
{
    public const string PacketBank = "2795110";
    public const uint Callback = 0x8DDEC0;

    public static NativeBattleFullscreenOverlayStep Advance8DDEC0(NativeBattleFullscreenOverlayFrame frame, uint address, uint pause2795914, NativeBattleFullscreenOverlayProgram program)
    {
        return AdvanceProfiled(frame, address, pause2795914, program, false);
    }

    public static NativeBattleFullscreenOverlayStep Advance8DDC30(NativeBattleFullscreenOverlayFrame frame, uint address, uint pause2795914, NativeBattleFullscreenOverlayProgram program)
    {
        return AdvanceProfiled(frame, address, pause2795914, program, true);
    }

    private static NativeBattleFullscreenOverlayStep AdvanceProfiled(NativeBattleFullscreenOverlayFrame frame, uint address, uint pause2795914, NativeBattleFullscreenOverlayProgram program, bool delayedCaller)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != (delayedCaller ? 0x8DDC30u : Callback) || task.Payload.Length < 0x40 - 12 || task.ReadDword(0x34) != program.Address || program.Address == 0 || program.Commands.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("Native fullscreen callback requires its actual40-byte task and bounded program owner.");
        }
        NativeBattleFullscreenOverlayState retained = frame.Memory.FullscreenOverlay ?? throw new NotSupportedException("Native fullscreen static packet bytes require an explicit retained owner.");
        retained.Validate();
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (!delayedCaller || pause2795914 == 0)
                {
                    task = Add(task, 0x38, -1);
                    if (Word(task, 0x38) <= 0)
                    {
                        task = task.WriteWord(0x38, 0);
                        if (!delayedCaller)
                        {
                            ExecuteProgram();
                        }
                        task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
                    }
                }
                break;
            case 1:
                ExecuteProgram();
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException("Native fullscreen program stage is unsupported.");
        }
        if (pause2795914 == 0)
        {
            task = Add(task, 0x3C, 1);
        }
        task = Add(task, 0x24, 1);
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Memory = frame.Memory with { FullscreenOverlay = retained }, Pools = hierarchy.Pools }, complete ? 2 : 0);

        void ExecuteProgram()
        {
            int index = Word(task, 0x3A);
            uint command = ReadProgram(index);
            byte opcode = (byte)(command >> 24);
            if (opcode == 0xFF)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                return;
            }
            if (opcode == 0xFE)
            {
                if (Word(task, 0x24) >= (byte)(command >> 16) && pause2795914 == 0)
                {
                    task = Add(task, 0x3A, 1).WriteDword(0x30, ReadProgram(checked(index + 1)));
                }
            }
            else
            {
                task = task.WriteDword(0x30, command);
                if (pause2795914 == 0)
                {
                    task = Add(task, 0x3A, 1);
                }
            }
            Draw(task.ReadDword(0x30));
        }
        uint ReadProgram(int index)
        {
            if ((uint)index >= program.Commands.Length)
            {
                throw new InvalidDataException("Native fullscreen signed program index leaves its supplied DWORD owner.");
            }
            return program.Commands[index];
        }
        void Draw(uint color)
        {
            NativeBattleEffectArena bytes = retained.Packets;
            NativeBattleLogicalOrdering.Builder links = new(frame.Ordering);
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                int x = (quadrant & 1) * 160;
                int y = (quadrant >> 1) * 120;
                Insert(quadrant * 24, [0x05000000, 0x2A000000 | (color & 0xFFFFFF), Screen(x, y), Screen(x + 160, y), Screen(x, y + 120), Screen(x + 160, y + 120)]);
            }
            // The shared45C690(0,1,640,0)/45BFC0 call has this exact established E1 plus trailing NOP representation.
            Insert(96, [0x02000000, 0xE100002A, 0]);
            retained = retained with { Packets = bytes };
            frame = frame with { Ordering = links.ToImmutable() };

            void Insert(int offset, ImmutableArray<uint> words)
            {
                byte[] stores = new byte[words.Length * 4];
                for (int index = 0; index < words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(stores.AsSpan(index * 4), words[index]);
                }
                if (offset < 96)
                {
                    bytes = bytes.Write(offset, stores);
                }
                else
                {
                    bytes = bytes.Write(offset + 3, stores.AsSpan(3));
                }
                ImmutableArray<int> depth = links.ProxyCount < frame.Ordering.Proxies.Length ? frame.Ordering.Proxies[links.ProxyCount].VertexPayload4 : [];
                bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
                links.Prepend(7, new(PacketBank, 0, offset), words, 0, depth);
                if (linked)
                {
                    bytes = bytes.Write(offset, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
                }
            }
        }
    }

    private static uint Screen(int x, int y) => unchecked((uint)(ushort)x | ((uint)(ushort)y << 16));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(Word(task, offset) + value)));
}
