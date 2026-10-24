using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect207PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool preprocessed;
    private bool programme;
    private bool particles;
    private bool screen;
    private bool waiting;
    private bool retired;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleEffect207State state = current.NativeEffectMemory!.Effect207!;
        state.Validate();
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? root = tasks.Values.FirstOrDefault(task => task.Callback == RootCallback);
        int age = main is null ? -1 : main.ReadWord(12);
        int oldAge = before is null ? -1 : before.ReadWord(12);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            if (pool.DescriptorAddress == RootDescriptor)
            {
                Assert.Null(state.FixedBanks[RootBank]);
            }
            else
            {
                NativeBattleCureFamilyPool shape = CommonPools.Single(item => item.Descriptor == pool.DescriptorAddress);
                Assert.Equal(state.ReadDword(ArenaGlobal) + shape.Bank, pool.BankAddress);
                Assert.Equal(shape.Stride, pool.Stride);
                Assert.Null(state.CommonQueueStorage[shape.Descriptor]);
            }
        }
        if (root is not null && root.ReadWord(12) == 1)
        {
            Mark("effect207-before-root-clear-origin", current, cold);
        }
        if (main is not null)
        {
            Assert.Equal(ProgrammeAddress, main.ReadDword(ProgrammeWorkspaceOffset));
            Assert.InRange(main.ReadDword(ProgrammeWorkspaceOffset + 4), 0u, 80u);
            programme = true;
            NativeBattleEffect207Resources resources = effect.Binding.Effect207Resources!;
            if (!state.Read(MutableMeshAddress, MutableMeshLength).AsSpan().SequenceEqual(resources.OriginalCopiedMeshData.AsSpan()))
            {
                preprocessed = true;
                Mark("effect207-current-preprocessed-mesh", current, cold);
            }
            if (age is 0 or 1 or 30 or 31 or 32 or 33 or 48 or 49 or 53 or 54 or 57 or 58 or 61 or 62 or 64 or 79 or 80 or 81)
            {
                Mark("effect207-before-main-age-" + age, current, cold);
            }
            if (age == 81)
            {
                waiting = true;
                Mark("effect207-native-completion-byte-wait", current, cold);
            }
            if (age is 33 or 49 or 63)
            {
                Assert.Equal(32, state.Read(MatrixAddress, 32).Length);
                Assert.Equal(32, state.Read(ViewAddress, 32).Length);
            }
        }
        if (tasks.Values.Any(task => task.Callback == 0x6A5650))
        {
            particles = true;
            Mark("effect207-current-particle-pool", current, cold);
        }
        if (oldAge == 80 && (age == 81 || main is null))
        {
            Contacts++;
            Mark("effect207-current-contact-range", current, cold);
        }
        if (before is not null && main is null)
        {
            retired = true;
            Mark("effect207-main-retired", current, cold);
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics!;
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
                Mark("effect207-current-contact-glyph", current, cold);
            }
        }
        CurrentCommands = Private(current, state);
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
        }
        if (oldAge is >= 32 and < 80 && state.ReadWord(MatrixAddress) != 0 && state.ReadWord(MatrixAddress + 8) != 0 && CurrentCommands.Any(command => command.Opcode is 0x38 or 0x3A) && CurrentCommands.Any(command => command.Opcode == 0xE1 && (command.Words[0] & 0xFFFFFF) == 0x240))
        {
            screen = true;
            Mark("effect207-current-screen-warp", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, CopiedMeshChanged = preprocessed, Programme = programme, Particles = particles, Screen = screen, Commands = CurrentCommands.Length, Contacts, ContactGlyphs });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(preprocessed);
        Assert.True(programme);
        Assert.True(particles);
        Assert.True(screen);
        Assert.True(waiting);
        Assert.True(retired);
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        NativeBattleEffect207State state = completed.NativeEffectMemory!.Effect207!;
        Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(state.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        File.WriteAllText(Path.Combine(output, "effect207-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Preprocessed = preprocessed, Programme = programme, Particles = particles, Screen = screen, Waiting = waiting, Retired = retired, Banks = banks.Order(), Opcodes = opcodes.Order(), Contacts, ContactGlyphs, Observations = observations }));
    }

    private NativeBattleLogicalCommand[] Private(BattleSnapshot current, NativeBattleEffect207State state)
    {
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint first = arena + 0x1A2C;
        if (arena == 0 || cursor < first)
        {
            return [];
        }
        uint start = cursor >= first + 0x10000 ? first + 0x10000 : first;
        Assert.InRange(cursor, start, start + 0x10000);
        NativeBattleLogicalCommand[] commands = current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).ToArray();
        if (commands.Length > 0)
        {
            banks.Add(start == first ? 0 : 1);
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
                Assert.True(visited.Add(selected));
                NativeBattleModulePoolTask task = pool.Slots[selected];
                result.Add(pool.BankAddress + (uint)(selected * pool.Stride), task);
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
