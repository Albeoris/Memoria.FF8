using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect173PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<int> centroidBanks = [];
    private readonly HashSet<int> recordKinds = [];
    private readonly List<object> observations = [];
    private bool commonInitialized;
    private bool programme;
    private bool geometry;
    private bool cleanup;
    private bool mainRetired;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        if (graphics.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect173State state = current.NativeEffectMemory!.Effect173!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main is null ? -1 : unchecked((short)main.ReadWord(12));
        int oldAge = before is null ? -1 : unchecked((short)before.ReadWord(12));
        Assert.Equal((byte)29, graphics.Models[3].ModelId4);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            if (pool.DescriptorAddress == CommonDescriptor)
            {
                Assert.Equal(state.ReadDword(ArenaGlobal) + CommonBankOffset, pool.BankAddress);
                Assert.Equal(CommonStride, pool.Stride);
                Assert.Equal(CommonCount, pool.Slots.Length);
                Assert.Null(state.CommonQueueStorage);
                commonInitialized = true;
            }
            else
            {
                NativeBattleCureFamilyPool shape = FixedPools.Single(item => item.Descriptor == pool.DescriptorAddress);
                Assert.Equal(shape.Bank, pool.BankAddress);
                Assert.Equal(shape.Stride, pool.Stride);
                Assert.Equal((int)shape.Capacity, pool.Slots.Length);
                Assert.Null(state.FixedBanks[shape.Bank]);
            }
        }
        if (!effect.Pools.Any(pool => pool.DescriptorAddress == CommonDescriptor))
        {
            Assert.NotNull(state.CommonQueueStorage);
        }
        if (age is 0 or 1 or 2 or 8 or 14 or 36 or 37 or 38 or 39 or 69 or 70 or 71 or 74 or 85 or 86 or 87 or 95)
        {
            Mark("effect173-before-main-age-" + age, current, cold);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Values)
        {
            callbacks.Add(task.Callback);
            if (task.Callback == 0x5D90B0)
            {
                uint frame = task.ReadDword(ProgrammeWorkspaceOffset + 4);
                Assert.InRange(frame, 0u, 6u);
                Assert.Equal(ProgrammeAddress, task.ReadDword(ProgrammeWorkspaceOffset));
                programme = true;
                if (frame is 1 or 5)
                {
                    Mark("effect173-current-programme-frame-" + frame, current, cold);
                }
            }
            if (task.Callback == 0x5D83E0)
            {
                int index = task.ReadWord(14);
                Assert.InRange(index, 0, 4);
                uint bank = FaceRecordAddresses[index];
                Assert.Equal(FaceRecordLengths[index], state.Read(bank, FaceRecordLengths[index]).Length);
                centroidBanks.Add(index);
                if (task.ReadWord(12) is 1 or 11)
                {
                    Mark("effect173-centroid-bank-" + index + "-age-" + task.ReadWord(12), current, cold);
                }
            }
            if (task.Callback == CleanupCallback && task.ReadWord(12) == 1)
            {
                cleanup = true;
                Mark("effect173-before-common-texture-byte-request", current, cold);
            }
        }
        for (int index = 0; index < FirstRecordCount; index++)
        {
            uint marker = state.ReadDword(FirstRecordsAddress + (uint)(index * FirstRecordStride));
            foreach (int kind in new[] { 1, 2, 4 })
            {
                if ((marker & kind) != 0 && recordKinds.Add(kind))
                {
                    Mark("effect173-current-record-kind-" + kind, current, cold);
                }
            }
        }
        if (oldAge == 85 && age == 86)
        {
            Contacts++;
            Mark("effect173-current-contact-range", current, cold);
        }
        if (oldAge == 95 && main is null)
        {
            mainRetired = true;
            Mark("effect173-main-retired-stage-reset", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect173-current-contact-glyph", current, cold);
            }
        }
        CurrentCommands = PrivateCommands(current, state);
        if (CurrentCommands.Any(command => command.Opcode is 0x24 or 0x26 or 0x2C or 0x2E or 0x30 or 0x32 or 0x38 or 0x3A or 0x34 or 0x36 or 0x3C or 0x3E))
        {
            geometry = true;
            Mark("effect173-current-private-geometry", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Pools = effect.Pools.Length, CommonInitialized = commonInitialized, Programme = programme, CentroidBanks = centroidBanks.Order().ToArray(), RecordKinds = recordKinds.Order().ToArray(), Commands = CurrentCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(commonInitialized);
        Assert.True(programme);
        Assert.True(geometry);
        Assert.True(cleanup);
        Assert.True(mainRetired);
        Assert.Equal(5, centroidBanks.Count);
        Assert.Equal(new[] { 1, 2, 4 }, recordKinds.Order());
        Assert.Equal(2, packetBanks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        NativeBattleEffect173State state = completed.NativeEffectMemory!.Effect173!;
        Assert.NotNull(state.CommonQueueStorage);
        Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
        File.WriteAllText(Path.Combine(output, "effect173-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), CommonInitialized = commonInitialized, Programme = programme, Geometry = geometry, Cleanup = cleanup, MainRetired = mainRetired, CentroidBanks = centroidBanks.Order(), RecordKinds = recordKinds.Order(), Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect173State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (arena == 0 || cursor <= arena)
        {
            return [];
        }
        uint start = cursor >= arena + 0xC000 ? arena + 0xC000 : arena;
        Assert.InRange(cursor, start, start + 0xC000);
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            packetBanks.Add(start == arena ? 0 : 1);
        }
        return commands;
    }

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
