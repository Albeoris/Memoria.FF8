using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect68PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<int> pages = [];
    private readonly List<object> observations = [];
    private bool mutableMeshes;
    private bool morphed;
    private bool camera;
    private bool results;
    private bool window;
    private bool automaticPage;
    private bool closedWindow;
    private bool geometry;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        NativeBattleOriginalEffectState? effect = graphics.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect68State state = current.NativeEffectMemory!.Effect68!;
        NativeBattleScanProcess scan = current.NativeEffectMemory.Scan!;
        state.Validate();
        scan.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> before = Tasks(prior);
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Pools.Single(item => item.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal((int)shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks)
        {
            callbacks.Add(task.Callback);
            int phase = task.ReadByte(0x29);
            if (task.Callback == RootCallback)
            {
                Mark("effect68-root-phase-" + phase, current, cold);
            }
            if (task.Callback == 0x7DD950 && phase > 0)
            {
                camera = true;
                Mark("effect68-current-camera-interpolation", current, cold);
            }
            if (task.Callback == 0x7DCBE0)
            {
                Mark("effect68-morph-phase-" + phase, current, cold);
            }
            if (task.Callback == 0x7DD650 && task.ReadDword(0x4C) == SpriteAddresses[1])
            {
                Mark("effect68-second-sprite-current-frame", current, cold);
            }
            if (task.Callback == 0x7DE540)
            {
                ObserveResults(current, address, task, before, state, scan, cold);
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask previous) in before)
        {
            if (previous.Callback == DriverCallback && previous.ReadByte(0x29) == 1 && !tasks.ContainsKey(address))
            {
                Contacts++;
                Mark("effect68-current-contact-after-children", current, cold);
            }
        }
        if (state.ReadWord(ResultCompleteGlobal) == 1 && prior.NativeEffectMemory?.Effect68?.ReadWord(ResultCompleteGlobal) == 0)
        {
            closedWindow = true;
            Assert.Equal(0u, scan.Text.Windows.TextRows[1].ReadDword(8));
            Mark("effect68-window-cleared-native-result-complete", current, cold);
        }
        if (state.MeshSourcesBound)
        {
            mutableMeshes = true;
            foreach (uint address in MutableMeshAddresses)
            {
                NativeBattleScanByteOwner bytes = scan.MutableMeshes[address];
                Assert.Equal(effect.Binding.Effect68Resources!.ReadMesh(address).Bytes.Length, bytes.Bytes.Length);
                Assert.All(bytes.Known, known => Assert.True(known));
                if (!bytes.Bytes.SequenceEqual(effect.Binding.Effect68Resources!.ReadMesh(address).Bytes))
                {
                    morphed = true;
                    Mark("effect68-current-mutable-mesh-" + address.ToString("X"), current, cold);
                }
            }
            Mark("effect68-canonical-mesh-source-publication", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect68-contact-glyph", current, cold);
            }
        }
        CurrentCommands = PrivateCommands(current, state);
        if (CurrentCommands.Any(command => command.Opcode is 0x20 or 0x22 or 0x24 or 0x26 or 0x28 or 0x2A or 0x2C or 0x2E or 0x30 or 0x32 or 0x38 or 0x3A))
        {
            geometry = true;
            Mark("effect68-current-private-geometry", current, cold);
        }
        if (effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            Assert.Equal(0u, scan.Text.Windows.TextRows[1].ReadDword(8));
            Assert.Equal((ushort)1, state.ReadWord(ResultCompleteGlobal));
            Assert.Equal((ushort)0, state.ReadWord(MainFlagGlobal));
            Assert.Equal((ushort)1, state.ReadWord(ContactFlagGlobal));
            Mark("effect68-retained-native-completion", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Pools = effect.Pools.Length, Tasks = tasks.Count, CurrentPrivatePackets = CurrentCommands.Length, Cursor = state.ReadDword(CursorGlobal), ResultCount = state.ReadDword(ResultCountAddress), ResultComplete = state.ReadWord(ResultCompleteGlobal), SharedRingCursor = scan.Results.Cursor, WindowText = scan.Text.Windows.TextRows[1].ReadDword(8), Contacts, ContactGlyphs });
    }

    private void ObserveResults(BattleSnapshot current, uint address, NativeBattleModulePoolTask task, Dictionary<uint, NativeBattleModulePoolTask> before, NativeBattleEffect68State state, NativeBattleScanProcess scan, Action<string, BattleSnapshot> cold)
    {
        int phase = task.ReadByte(0x29);
        int page = task.ReadWord(0x6C);
        int count = task.ReadWord(0x6A);
        if (phase > 0)
        {
            results = true;
            Assert.Equal((uint)count, state.ReadDword(ResultCountAddress));
            Assert.InRange(count, 1, 8);
            Assert.Equal((byte)0, state.Read(ResultEmptyAddress, 1)[0]);
            scan.Results.Scratch48.RequireLength(48);
            scan.Results.Ring768.RequireLength(768);
            Assert.All(scan.Results.Scratch48.Known, known => Assert.True(known));
            Assert.All(scan.Results.Ring768.Known, known => Assert.True(known));
            Mark("effect68-current-private-header-shared-scratch", current, cold);
        }
        if (phase == 2)
        {
            window = true;
            pages.Add(page);
            Assert.NotEqual(0u, scan.Text.Windows.TextRows[1].ReadDword(8));
            Assert.InRange(page, 0, count - 1);
            Mark("effect68-open-page-" + page, current, cold);
            if (task.ReadWord(0x66) == 1)
            {
                Mark("effect68-before-page-timeout-" + page, current, cold);
            }
        }
        if (before.TryGetValue(address, out NativeBattleModulePoolTask? previous) && previous.Callback == task.Callback && previous.ReadByte(0x29) == 2 && previous.ReadWord(0x66) == 1 && task.ReadWord(0x6C) == previous.ReadWord(0x6C) + 1 && (task.ReadDword(0x5C) & 0x60) == 0)
        {
            automaticPage = true;
            Mark("effect68-native-45-tick-page", current, cold);
        }
        if (phase >= 4)
        {
            closedWindow = true;
            Assert.Equal(0u, scan.Text.Windows.TextRows[1].ReadDword(8));
            Assert.Equal((ushort)1, state.ReadWord(ResultCompleteGlobal));
            Mark("effect68-window-cleared-native-result-complete", current, cold);
        }
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(mutableMeshes);
        Assert.True(morphed);
        Assert.True(camera);
        Assert.True(results);
        Assert.True(window);
        Assert.True(automaticPage);
        Assert.True(closedWindow);
        Assert.True(geometry);
        Assert.NotEmpty(pages);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.Contains("effect68-retained-native-completion", markers);
        File.WriteAllText(Path.Combine(output, "effect68-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Pages = pages.Order(), PacketBanks = packetBanks.Order(), MutableMeshes = mutableMeshes, Morphed = morphed, Camera = camera, Results = results, Window = window, AutomaticPage = automaticPage, ClosedWindow = closedWindow, Geometry = geometry, Contacts, ContactGlyphs, completed.Frame, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] PrivateCommands(BattleSnapshot current, NativeBattleEffect68State state)
    {
        uint first = state.ReadDword(FirstPacketGlobal);
        uint second = state.ReadDword(SecondPacketGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (first == 0 || second == 0 || cursor == 0)
        {
            return [];
        }
        uint start = cursor >= second ? second : first;
        Assert.InRange(cursor, start, start + 0x4000);
        int begin = checked((int)(start - 0x20DFAB8u));
        int end = checked((int)(cursor - 0x20DFAB8u));
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= begin && command.Packet.ByteOffset < end).ToArray();
        if (commands.Length != 0)
        {
            packetBanks.Add(start == first ? 0 : 1);
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
