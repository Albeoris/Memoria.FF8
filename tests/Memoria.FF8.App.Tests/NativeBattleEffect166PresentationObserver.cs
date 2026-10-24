using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect166Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect166PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool paths;
    private bool history;
    private bool ribbon;
    private bool records;
    private bool capture;
    private bool geometry;
    private bool sequence6;
    private bool cleanup;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentFadeCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (graphics.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect166State state = current.NativeEffectMemory!.Effect166!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        Assert.Equal((byte)130, graphics.Models[3].ModelId4);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal((int)shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            callbacks.Add(task.Callback);
            if (task.Callback == 0x5E5D50)
            {
                Assert.InRange(task.ReadWord(14), 0, PathCount - 1);
                paths = true;
            }
            if (task.Callback == 0x5E7AE0 && task.ReadWord(12) >= 2)
            {
                history = true;
                Mark("effect166-current-path-history", current, cold);
            }
            if (task.Callback == 0x5E7ED0 && task.ReadWord(0x18) >= 24)
            {
                ribbon = true;
                Mark("effect166-current-derived-ribbon", current, cold);
            }
            if (task.Callback == CleanupCallback && task.ReadWord(12) == 1)
            {
                cleanup = true;
                Mark("effect166-before-common-texture-byte-request", current, cold);
            }
        }
        if (age is 0 or 1 or 8 or 9 or 27 or 35 or 43 or 44 or 51 or 54 or 55 or 56 or 90 or 98)
        {
            Mark("effect166-before-main-age-" + age, current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous)
        {
            if (task.Callback == 0x5E8EF0 && unchecked((short)task.ReadWord(0x1A)) >= 0 && !tasks.ContainsKey(address))
            {
                Contacts++;
                Mark("effect166-restored-model-current-contact", current, cold);
            }
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect166-current-contact-glyph", current, cold);
            }
        }
        if (graphics.Models[0].Sequence == 6)
        {
            sequence6 = true;
            Mark("effect166-current-recipient-sequence6", current, cold);
        }
        int first = Enumerable.Range(0, FirstRecordCount).Count(index => state.ReadDword(FirstRecordsAddress + (uint)(index * FirstRecordStride)) != 0);
        int second = Enumerable.Range(0, SecondRecordCount).Count(index => state.ReadDword(SecondRecordsAddress + (uint)(index * SecondRecordStride)) != 0);
        if (first > 0 || second > 0)
        {
            records = true;
            Mark("effect166-current-particle-records", current, cold);
        }
        NativeBattleLogicalCommand[] privateCommands = PrivateCommands(current, state);
        CurrentFadeCommands = Commands(current).Where(command => Common(command) && (command.Opcode is 0x60 or 0x62) && command.Words.Length == 3 && command.Words[1] == 0 && command.Words[2] == 0x00D80140).ToArray();
        NativeBattleLogicalCommand[] environment = [];
        if (oldAge == 43)
        {
            byte[] source = state.ReadCapturePacket().Read(0, CapturePacketLength);
            Assert.Equal(effect.Binding.Effect166Resources!.ReadOriginalCapturePacket().AsSpan().Slice(4).ToArray(), source.AsSpan(4).ToArray());
            environment = Commands(current).Where(command => command.Packet.Bank == "23BF038" && command.Opcode is >= 0xE3 and <= 0xE5).ToArray();
            Assert.NotEmpty(environment);
            byte[] beforeVram = prior.NativeEffectMemory!.Textures!.ReadVram();
            byte[] afterVram = current.NativeEffectMemory!.Textures!.ReadVram();
            int changedPixels = 0;
            int nonzeroPixels = 0;
            for (int y = 256; y < 512; y++)
            {
                for (int x = 384; x < 640; x++)
                {
                    int offset = (y * 1024 + x) * 2;
                    ushort beforePixel = BinaryPrimitives.ReadUInt16LittleEndian(beforeVram.AsSpan(offset));
                    ushort afterPixel = BinaryPrimitives.ReadUInt16LittleEndian(afterVram.AsSpan(offset));
                    changedPixels += beforePixel != afterPixel ? 1 : 0;
                    nonzeroPixels += afterPixel != 0 ? 1 : 0;
                }
            }
            Assert.True(changedPixels > 100, "The current software stage capture did not change its target VRAM rectangle.");
            Assert.True(nonzeroPixels > 100, "The captured current stage contains no visible geometry after its black clear.");
            Assert.Equal(1u, current.NativeEffectMemory.PrimitiveProcess!.Software!.Environment.Derived!.Mode);
            capture = true;
            Mark("effect166-current-stage-capture-environment", current, cold);
        }
        CurrentCommands = privateCommands.Concat(environment).Concat(CurrentFadeCommands).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
        if (privateCommands.Any(command => command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E or 0x30 or 0x32 or 0x38 or 0x3A or 0x34 or 0x36 or 0x3C or 0x3E))
        {
            geometry = true;
            Mark("effect166-current-private-geometry", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Pools = effect.Pools.Length, FirstRecords = first, SecondRecords = second, PrivateCommands = privateCommands.Length, EnvironmentCommands = environment.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(paths);
        Assert.True(history);
        Assert.True(ribbon);
        Assert.True(records);
        Assert.True(capture);
        Assert.True(geometry);
        Assert.True(sequence6);
        Assert.True(cleanup);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Contains(MainCallback, callbacks);
        Assert.Equal(2, packetBanks.Count);
        File.WriteAllText(Path.Combine(output, "effect166-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), Paths = paths, History = history, Ribbon = ribbon, Records = records, CurrentStageCapture = capture, PrivateGeometry = geometry, CurrentRecipientSequence6 = sequence6, CommonTextureRequest = cleanup, Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect166State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        uint start = cursor >= arena + 0xC000 ? arena + 0xC000 : arena;
        Assert.InRange(cursor, start, start + 0xC000);
        NativeBattleLogicalCommand[] commands = Commands(current).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

    private static bool Common(NativeBattleLogicalCommand command) => (command.Packet.Bank is "off_B6D070" or "off_B6D094") && command.WordOffset == 1;
    private static IEnumerable<NativeBattleLogicalCommand> Commands(BattleSnapshot snapshot) => snapshot.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command);
    private static Dictionary<uint, NativeBattleModulePoolTask> Tasks(BattleSnapshot snapshot)
    {
        Dictionary<uint, NativeBattleModulePoolTask> result = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return result;
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            HashSet<int> visited = [];
            int? index = pool.Head;
            while (index is int selected)
            {
                Assert.InRange(selected, 0, pool.Slots.Length - 1);
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(checked(pool.BankAddress + (uint)(selected * pool.Stride)), task);
                index = task.Next;
            }
        }
        return result;
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
