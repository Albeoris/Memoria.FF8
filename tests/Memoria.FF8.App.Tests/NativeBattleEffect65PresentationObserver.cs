using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect65PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool programme;
    private bool morph;
    private bool currentFile394;
    private bool warmLiterals;
    private bool privateRandom;
    private bool flash;
    private bool sequence13;
    private bool childQueueRetained;
    private bool commonPackets;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect65State state = current.NativeEffectMemory!.Effect65!;
        state.Validate();
        Assert.Equal((byte)140, graphics.Models[5].ModelId4);
        Assert.Equal((ushort)1448, current.NativeModelCpuResources!.ScratchMaximum);
        NativeBattleModulePoolTask? before = ReadMain(prior);
        NativeBattleModulePoolTask? main = ReadMain(current);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        if (graphics.Models[5].Sequence == 13)
        {
            sequence13 = true;
            Mark("effect65-sequence13", current, cold);
        }
        if (before?.ReadWord(12) == 548 && main?.ReadWord(12) == 549)
        {
            Contacts++;
            Mark("effect65-current-counted-contact", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect65-contact-glyph", current, cold);
            }
        }
        if (effect.ReturnedDescriptor > 0)
        {
            Assert.InRange(effect.Pools.Length, 1, 3);
            Assert.Null(state.RootBankStorage);
            if (effect.Pools.Length == 3)
            {
                Assert.Null(state.ChildQueueStorage);
            }
            else
            {
                Assert.Equal(0u, state.ReadDword(ChildDescriptor));
                Assert.Equal(0u, state.ReadDword(ChildDescriptor + 4));
            }
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                NativeBattleCureFamilyPool shape = Pools[index];
                Assert.Equal(shape.Descriptor, pool.DescriptorAddress);
                Assert.Equal(index == 0 ? RootBank : state.ReadDword(ArenaGlobal) + shape.Bank, pool.BankAddress);
                Assert.Equal(shape.Stride, pool.Stride);
                Assert.Equal((int)shape.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in Live(pool))
                {
                    callbacks.Add(task.Callback);
                }
            }
            if (age is 31 or 42 or 55 or 166 or 237 or 238 or 342 or 402 or 472 or 542 or 543 or 548 or 552)
            {
                Mark("effect65-main-age-" + age, current, cold);
            }
            if (age >= 473 && effect.Pools.Length == 3)
            {
                NativeBattleEffectTaskPool child = effect.Pools[2];
                Assert.Null(child.Head);
                Assert.Null(child.Tail);
                childQueueRetained |= child.Slots.Any(task => task.Callback != 0);
                Mark("effect65-cleared-child-queue-retained-slots", current, cold);
            }
            if (age is >= 43 and <= 167 or >= 239 and <= 342 or >= 473 and <= 542)
            {
                uint source = main!.ReadDword(0x48);
                Assert.InRange(source, checked(state.ReadDword(ArenaGlobal) + FileOffset), checked(state.ReadDword(ArenaGlobal) + FileOffset + 94151));
                Assert.True(main!.ReadDword(0x4C) > 0);
                programme = true;
                Mark("effect65-current-loaded-ACT", current, cold);
            }
            if (age is >= 239 and <= 316)
            {
                morph = true;
                Mark("effect65-current-morph", current, cold);
            }
            if (age >= 238)
            {
                NativeBattleEffectArena arena = current.NativeEffectMemory!.Arena!;
                uint polygon = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x39E00, 4));
                uint vertices = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x39E04, 4));
                Assert.Equal(104u, polygon);
                Assert.Equal(12u, vertices);
                currentFile394 = true;
                Mark("effect65-current-file394-raw-banks", current, cold);
            }
            if (LiteralRanges.Any(range => state.Read(range.Address, range.Length).Any(value => value != 0)))
            {
                warmLiterals = true;
                Mark("effect65-current-literal-fields", current, cold);
            }
            if (state.ReadDword(RandomGlobal) != prior.NativeEffectMemory!.Effect65!.ReadDword(RandomGlobal))
            {
                privateRandom = true;
                Mark("effect65-private-LCG", current, cold);
            }
        }
        NativeBattleLogicalCommand[] strips = CommonStripCommands(current, age);
        if (strips.Length > 0)
        {
            commonPackets = true;
            Mark("effect65-current-COMMON-linked-strips", current, cold);
        }
        CurrentFadeCommands = CommonFadeCommands(current, before);
        CurrentCommands = PrivateCommands(current).Concat(strips).Concat(CurrentFadeCommands).DistinctBy(command => command.Packet).ToArray();
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
            if (command.Packet.Bank == "20DFAB8")
            {
                int bank = (command.Packet.ByteOffset - FirstPacketOffset) / 0x10000;
                packetBanks.Add(bank);
            }
            if (command.Opcode == 0x2A && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == 0x00D80000 && command.Words[4] == 0x00D80140)
            {
                flash = true;
                Mark("effect65-fullviewport-flash", current, cold);
            }
        }
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.NotNull(state.RootBankStorage);
            Assert.Equal(0u, state.ReadDword(ChildDescriptor));
            Assert.Equal(0u, state.ReadDword(ChildDescriptor + 4));
            Assert.Equal(0u, graphics.Models[5].ScaleVectorAddress48);
            Assert.Equal(0u, graphics.Models[0].ScaleVectorAddress48);
            Mark("effect65-retained-native-completion", current, cold);
        }
        observations.Add(new { current.Frame, Callback = graphics.Callback, Age = age, Sequence = graphics.Models[5].Sequence, Cursor = state.ReadDword(CursorGlobal), PrivateRandom = state.ReadDword(RandomGlobal), Pools = effect.Pools.Length, OwnedPackets = CurrentCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(sequence13);
        Assert.True(programme);
        Assert.True(morph);
        Assert.True(currentFile394);
        Assert.True(warmLiterals);
        Assert.True(privateRandom);
        Assert.True(flash);
        Assert.True(childQueueRetained);
        Assert.True(commonPackets);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Contains("effect65-retained-native-completion", markers);
        File.WriteAllText(Path.Combine(output, "effect65-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), Programme = programme, Morph = morph, CurrentFile394 = currentFile394, WarmLiteralFields = warmLiterals, PrivateRandom = privateRandom, FullviewportFlash = flash, RetainedChildQueue = childQueueRetained, CommonPackets = commonPackets, Contacts, ContactGlyphs, Observations = observations }));
    }

    private static NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current)
    {
        NativeBattleEffect65State state = current.NativeEffectMemory!.Effect65!;
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena + FirstPacketOffset)
        {
            return [];
        }
        int end = checked((int)(cursor - arena));
        int start = FirstPacketOffset + Math.Clamp((end - FirstPacketOffset - 1) / 0x10000, 0, 1) * 0x10000;
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end).ToArray();
    }

    private static NativeBattleLogicalCommand[] CommonStripCommands(BattleSnapshot current, int age)
    {
        if (age is < 403 or > 472 || current.NativeEffectMemory!.Effect65!.Read(0x2544FD0, 1)[0] == 0)
        {
            return [];
        }
        NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
        bool linked = Enumerable.Range(0, 32).Any(index => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(0x3CD30 + index * 56, 4)) != 0 && BinaryPrimitives.ReadInt16LittleEndian(arena.Read(0x3CD30 + index * 56 + 0x34, 2)) >= 2);
        if (!linked)
        {
            return [];
        }
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command =>
            (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1 && command.Opcode == 0x3E && command.Flags20 == 1 && command.Words.Length == 12 && command.VertexPayload4.Length == 4 && command.VertexPayload4.All(value => value == 0) && command.Words[2] == 0x3F54A048 && command.Words[5] == 0x00B6A078 && (ushort)command.Words[8] == 0xA048 && (ushort)command.Words[11] == 0xA078 && (command.Words[0] & 0xFFFFFF) == (command.Words[3] & 0xFFFFFF) && (command.Words[6] & 0xFFFFFF) == (command.Words[9] & 0xFFFFFF)).ToArray();
    }

    private static NativeBattleModulePoolTask? ReadMain(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == Effect ? snapshot.NativeGraphics.OriginalEffect.Pools.SelectMany(Live).FirstOrDefault(task => task.Callback == MainCallback) : null;

    private static NativeBattleLogicalCommand[] CommonFadeCommands(BattleSnapshot current, NativeBattleModulePoolTask? before)
    {
        if (before is null)
        {
            return [];
        }
        int age = unchecked((short)before.ReadWord(12));
        int factor;
        if (age is >= 466 and < 497)
        {
            factor = NativeBattleModuleRenderCommands.Factor700150(age - 466, 4, 2, 25);
        }
        else if (age is >= 532 and < 552)
        {
            factor = NativeBattleModuleRenderCommands.Factor700150(age - 532, 8, 4, 8);
        }
        else
        {
            return [];
        }
        uint color = unchecked((byte)(255 * factor >> 12));
        uint packed = 0x2A000000 | color | color << 8 | color << 16;
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1 && command.Opcode == 0x2A && command.Flags20 == 0 && command.Words.Length == 5 && command.Words[0] == packed && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == 0x00D80000 && command.Words[4] == 0x00D80140).ToArray();
    }

    private static IEnumerable<NativeBattleModulePoolTask> Live(NativeBattleEffectTaskPool pool)
    {
        HashSet<int> visited = [];
        int? index = pool.Head;
        while (index is int selected)
        {
            Assert.InRange(selected, 0, pool.Slots.Length - 1);
            Assert.True(visited.Add(selected));
            NativeBattleModulePoolTask task = pool.Slots[selected];
            yield return task;
            index = task.Next;
        }
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
