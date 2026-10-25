using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleSelectedCinematicObserver(BattleSnapshot source, ushort selectedEffect)
{
    private readonly HashSet<string> markers = [];
    private readonly List<object> observations = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> publish)
    {
        Assert.NotNull(source.NativeGraphics);
        NativeBattleOriginalEffectState? effect = current.NativeGraphics?.OriginalEffect;
        if (effect?.Binding.Effect != selectedEffect || effect?.ReturnedDescriptor is not > 0)
        {
            return;
        }
        NativeBattleScriptedEffectMemory core = current.NativeEffectMemory!.ScriptedEffects!;
        NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
        uint manager = Dword(core, 0x27973EC);
        uint bank = Dword(core, manager + 0x90);
        uint basis = Dword(core, 0x2798A68);
        uint header = Dword(core, 0x279744C);
        int capacity = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(header + 0x38, 2));
        byte[] order = core.Read(0x2797554, 128);
        int terminal = Array.IndexOf(order, (byte)255);
        Assert.InRange(terminal, 0, 127);
        List<object> slots = [];
        List<string> transitions = [];
        for (int position = 0; position < terminal; position++)
        {
            byte index = order[position];
            Assert.InRange(index, 0, capacity - 1);
            byte[] raw = arena.Read(checked((int)(bank + index * 256u - 0x20DFAB8)), 256);
            byte kind = raw[0x1C];
            uint shape = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xD8));
            uint retained = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xBC));
            slots.Add(new { Index = index, Kind = kind, Shape = shape, Retained = retained, Flags = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x4C)), Light = raw[0xE1] });
            if (kind == 2 && shape >= basis)
            {
                transitions.Add($"mesh-{shape - basis}-{(retained == 0 ? "base" : "morph")}");
            }
            if (kind == 21)
            {
                transitions.Add("draw-21");
                if (BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x96)) != 0)
                {
                    transitions.Add("draw-21-scroll");
                }
            }
            if (raw[0x18] == 9)
            {
                transitions.Add("update-9");
            }
            if (kind == 37)
            {
                transitions.Add("draw-37");
                if (retained != 0 && BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x90)) != 0)
                {
                    ushort visible = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(checked((int)(retained + 4 - 0x20DFAB8)), 2));
                    int commonStart = prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor ?? 0;
                    if (visible >= 2 && CurrentCommands(current, commonStart).Any(command => (command.Opcode & 0xFC) == 0x38))
                    {
                        transitions.Add("draw-37-live");
                    }
                }
            }
            if (kind is 26 or 43 or 53)
            {
                transitions.Add($"draw-{kind}");
            }
            if (kind == 43 && retained != 0 && BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x94)) >= 1)
            {
                int commonStart = prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor ?? 0;
                if (CurrentCommands(current, commonStart).Any(command => (command.Opcode & 0xFC) is 0x2C or 0x3C))
                {
                    transitions.Add("draw-43-live");
                }
            }
            if (kind == 53 && retained != 0)
            {
                uint payload = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB8));
                int count = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(checked((int)(payload + 4 - 0x20DFAB8)), 2));
                Assert.InRange(count, 1, 4096);
                for (int particle = 0; particle < count; particle++)
                {
                    if ((arena.Read(checked((int)(retained + 16 + 28u * (uint)particle + 24 - 0x20DFAB8)), 1)[0] & 128) == 0)
                    {
                        transitions.Add("draw-53-live");
                        break;
                    }
                }
            }
            if (kind == 26 && BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xC4)) != 0 && raw[0xCC] == raw[0xCD] && raw[0xCD] == raw[0xCE])
            {
                int commonStart = prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor ?? 0;
                if (CurrentCommands(current, commonStart).Count(command => (command.Opcode & 0xFC) == 0x28) >= 5)
                {
                    transitions.Add("draw-26-live");
                }
            }
            if (kind == 34 && shape >= basis)
            {
                uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB8));
                Assert.True(descriptor >= basis);
                transitions.Add($"clamped-{shape - basis}-{descriptor - basis}");
            }
            if (kind == 27 && shape >= basis)
            {
                transitions.Add($"mesh27-{shape - basis}");
            }
            if (kind is 4 or 6)
            {
                transitions.Add($"draw-{kind}");
            }
            if (kind == 4 && Enumerable.Range(0, 3).Any(component => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(0x94 + 2 * component)) != 0))
            {
                int commonStart = prior.NativeEffectMemory!.PrimitiveProcess!.ByteCursor ?? 0;
                if (CurrentCommands(current, commonStart).Any(command => (command.Opcode & 0xFC) == 0x28))
                {
                    transitions.Add("draw-4-positive");
                }
            }
            if (kind == 6)
            {
                uint pool = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB8));
                if (pool != 0)
                {
                    int particleCount = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(checked((int)(pool - 0x20DFAB8)), 2));
                    Assert.InRange(particleCount, 0, 128);
                    for (int particle = 0; particle < particleCount; particle++)
                    {
                        byte[] record = arena.Read(checked((int)(pool + 16 + 80u * (uint)particle - 0x20DFAB8)), 80);
                        if (BinaryPrimitives.ReadUInt16LittleEndian(record) != 0 && BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x30)) != 0)
                        {
                            transitions.Add("draw-6-live");
                            break;
                        }
                    }
                }
            }
        }
        NativeBattlePrimitiveProcess before = prior.NativeEffectMemory!.PrimitiveProcess!;
        NativeBattlePrimitiveProcess after = current.NativeEffectMemory.PrimitiveProcess!;
        Assert.NotNull(before.ByteCursor);
        Assert.Equal(before.PacketBank, after.PacketBank);
        Assert.Equal(before.PacketBuffer, after.PacketBuffer);
        Assert.All(prior.NativeGraphics!.ModuleEffects.Slots, task => Assert.Equal(0, task.Flags & 1));
        Assert.All(prior.NativeGraphics.Auxiliary.Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Contains(task.Kind, new[] { NativeBattleAuxiliaryKind.CameraBlend509930, NativeBattleAuxiliaryKind.StageFade501F90 }));
        Assert.All(prior.NativeGraphics.Descriptors.Queue, index => Assert.Equal((ushort)104, prior.NativeGraphics.Descriptors.Packets[index].Command));
        Assert.All(prior.NativeGraphics.Descriptors.Tasks.Where(task => (task.Flags & 1) != 0), task => Assert.Contains(task.Kind, new[] { NativeBattleDescriptorTaskKind.Pump500CC0, NativeBattleDescriptorTaskKind.Action104, NativeBattleDescriptorTaskKind.DeferredCompletion500BC0 }));
        observations.Add(new
        {
            current.Frame,
            CurrentGte = current.NativeEffectMemory.GeometryRegisters,
            CurrentColors = current.NativeEffectMemory.ScriptedColorRegisters,
            ArenaCursorStart = Dword(core, manager + 0xD4),
            ArenaCursorEnd = Dword(core, manager + 0xD8),
            ArenaPacketStart = Dword(core, manager + 0xD4),
            CommonPrefixStart = before.ByteCursor.Value,
            CommonPrefixEnd = Dword(core, manager + 0x7C),
            CommonBank = after.PacketBank,
            CommonBuffer = after.PacketBuffer,
            Entry = new { Cursor = before.ByteCursor.Value, Bank = before.PacketBank, Buffer = before.PacketBuffer, WriteCapacity = before.CommonPacketWriteCapacity },
            Slots = slots
        });
        foreach (string transition in transitions.Distinct())
        {
            if (markers.Add(transition))
            {
                publish(transition, current);
            }
        }
    }

    public void Write(string output)
    {
        int effect = selectedEffect;
        File.WriteAllText(Path.Combine(output, $"effect{effect}-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Observations = observations }));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot, int commonStart)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { ReturnedDescriptor: > 0 } || snapshot.NativeEffectMemory?.ScriptedEffects is not { } core || snapshot.NativeGraphics.PrimitiveFrame is not { } primitive)
        {
            return [];
        }
        uint manager = Dword(core, 0x27973EC);
        uint arenaStart = Dword(core, manager + 0xD4);
        uint arenaEnd = Dword(core, manager + 0xD8);
        NativeBattlePrimitiveProcess process = snapshot.NativeEffectMemory.PrimitiveProcess!;
        uint commonEnd = Dword(core, manager + 0x7C);
        Assert.InRange(commonEnd, 0u, checked((uint)process.CommonPacketWriteCapacity));
        int commonBase = process.PacketBuffer * process.CommonPacketNominalCapacity;
        return primitive.LastCommands.Select(item => item.Command).Where(IsOwned).ToArray();

        bool IsOwned(NativeBattleLogicalCommand command)
        {
            if (command.Packet.ByteOffset < 0 || command.Opcode is not (>= 0x20 and < 0x80) && command.Opcode != 0xE1)
            {
                return false;
            }
            if (command.Packet.Bank == "20DFAB8")
            {
                uint address = checked(0x20DFAB8u + (uint)command.Packet.ByteOffset);
                if (command.Packet.Buffer != 0 || arenaStart < 0x20DFAB8 || address < arenaStart || address >= arenaEnd)
                {
                    return false;
                }
                byte[] arenaTag = snapshot.NativeEffectMemory!.Arena!.Read(command.Packet.ByteOffset, 4);
                int length = checked((1 + arenaTag[3]) * 4);
                Assert.True(address + length <= arenaEnd);
                return true;
            }
            NativeBattlePrimitiveReference normalized = NativeBattlePrimitiveProcess.NormalizeCommonReference(command.Packet);
            if (normalized.Bank != process.PacketBank || normalized.Buffer != 0 || normalized.ByteOffset < commonBase || normalized.ByteOffset - commonBase < commonStart || (uint)(normalized.ByteOffset - commonBase) >= commonEnd)
            {
                return false;
            }
            NativeBattlePrimitiveProcess physical = process with { PacketBank = command.Packet.Bank, PacketBuffer = command.Packet.Buffer };
            byte[] tag = physical.ReadCommonPacket(command.Packet.ByteOffset, 4);
            int packetLength = checked((1 + tag[3]) * 4);
            Assert.True(normalized.ByteOffset - commonBase + packetLength <= commonEnd);
            return true;
        }
    }

    private static uint Dword(NativeBattleScriptedEffectMemory core, uint address)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
    }
}
