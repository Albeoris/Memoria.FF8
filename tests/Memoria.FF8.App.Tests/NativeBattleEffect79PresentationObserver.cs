using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect79PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<byte> rootPhases = [];
    private readonly HashSet<byte> mainPhases = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> relocated = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool contactHandshake;
    private bool commonBody;
    private bool scriptDestinations;
    private bool detached;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect79State state = current.NativeEffectMemory!.Effect79!;
        state.Validate();
        Assert.Equal((byte)138, current.NativeGraphics.Models[4].ModelId4);
        Assert.Equal((byte)139, current.NativeGraphics.Models[3].ModelId4);
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Equal(Pools.Length, effect.Pools.Length);
            Assert.All(state.FixedBanks.Values, bank => Assert.Null(bank));
            Assert.NotNull(state.ProgramData);
            Assert.Equal(ScriptLength, state.ProgramData!.Value.Length);
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleCureFamilyPool expected = Pools[index];
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                {
                    callbacks.Add(task.Callback);
                    if (task.Callback == RootCallback)
                    {
                        byte phase = task.ReadByte(0x29);
                        Assert.InRange(phase, (byte)0, (byte)12);
                        if (rootPhases.Add(phase) && phase is 3 or 6 or 7 or 8 or 9 or 10)
                        {
                            Mark("effect79-root-phase-" + phase, current, cold);
                        }
                    }
                    if (task.Callback == 0x7B4FF0)
                    {
                        byte phase = task.ReadByte(0x29);
                        Assert.InRange(phase, (byte)0, (byte)4);
                        Assert.Equal((byte)3, task.ReadByte(0x2D));
                        mainPhases.Add(phase);
                        NativeBattleModulePoolTask? previous = prior.NativeGraphics!.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(item => item.Callback == 0x7B4FF0);
                        if (phase == 2 && previous?.ReadByte(0x29) == 1 && !contactHandshake)
                        {
                            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Effect79!.ReadWord(TargetAddress + 0x48));
                            contactHandshake = true;
                            Mark("effect79-contact-handshake", current, cold);
                        }
                    }
                }
            }
            foreach ((uint address, ImmutableArray<byte> bytes) in state.NodeBanks)
            {
                Assert.Equal(NodeSourceLengths[address], bytes.Length);
                if (state.ReadDword(address) == 1 && relocated.Add(address))
                {
                    Mark("effect79-relocated-" + address.ToString("X"), current, cold);
                }
            }
            if (Enumerable.Range(1, 5).Any(index => state.ReadDword(ScriptAddress + (uint)(index * 16 + 12)) != 0) && !scriptDestinations)
            {
                scriptDestinations = true;
                Mark("effect79-current-io-script", current, cold);
            }
            ImmutableHashSet<NativeBattlePrimitiveReference> prefix = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
            if (!prefix.IsEmpty && !commonBody)
            {
                commonBody = true;
                Mark("effect79-current-common-prefix", current, cold);
            }
            foreach (NativeBattleLogicalCommand command in EffectCommands(prior, current))
            {
                opcodes.Add(command.Opcode);
                if (command.Packet.Bank == "20DFAB8")
                {
                    int initializer = checked((int)(state.ReadDword(InitializerGlobal) - 0x20DFAB8u));
                    int bank = (command.Packet.ByteOffset - initializer) / 0x8000;
                    Assert.InRange(bank, 0, 1);
                    if (packetBanks.Add(bank))
                    {
                        Mark("effect79-packet-bank-" + bank, current, cold);
                    }
                }
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.FixedBanks.Values, bank => Assert.NotNull(bank));
            detached = true;
            Mark("effect79-physical-banks-detached", current, cold);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, RootPhases = rootPhases.Order().ToArray(), TargetPhase = state.ReadWord(TargetAddress + 0x40), TargetAge = state.ReadWord(TargetAddress + 0x46), ContactFlag = state.ReadWord(TargetAddress + 0x48), BodyFlag = state.ReadWord(TargetAddress + 0x4C), EmitterCursor = state.ReadWord(0x25E8950), GeometryCursor = state.ReadWord(0x25EAF40), IoCursor = state.ReadDword(0x25EA348), Completion = state.Read(0x25DF340, 1)[0], Random = current.NativeCrtRandom, Relocated = relocated.Order().ToArray() });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.NotEmpty(relocated);
        Assert.True(scriptDestinations);
        Assert.True(commonBody);
        Assert.True(contactHandshake);
        Assert.True(detached);
        Assert.Contains((byte)8, rootPhases);
        Assert.Contains((byte)4, mainPhases);
        Assert.Contains(RootCallback, callbacks);
        Assert.Contains(0x7B51C0u, callbacks);
        Assert.Contains(0x7B82C0u, callbacks);
        Assert.Contains(0x7BC840u, callbacks);
        Assert.Contains(0x7BDAD0u, callbacks);
        Assert.NotEmpty(opcodes);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.NotNull(initialRandom);
        Assert.NotEqual(initialRandom, lastRandom);
        File.WriteAllText(Path.Combine(output, "effect79-observations.json"), JsonSerializer.Serialize(new { Scenario = "Declared post-formula death-action renderer and lifecycle; ordinary death-AI, KO, selector admission and formula execution are excluded", Markers = markers.Order(), RootPhases = rootPhases.Order(), MainPhases = mainPhases.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), Relocated = relocated.Order(), CurrentScript = scriptDestinations, CommonPrefix = commonBody, ContactHandshake = contactHandshake, Detached = detached, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot prior, BattleSnapshot current)
    {
        ImmutableHashSet<NativeBattlePrimitiveReference> prefix = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
        return EffectCommands(current).Concat(current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => prefix.Contains(item.Command.Packet)).Select(item => item.Command)).Distinct().ToArray();
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        NativeBattleEffect79State state = current.NativeEffectMemory!.Effect79!;
        uint source = state.ReadDword(InitializerGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (source == 0 || cursor == 0)
        {
            return [];
        }
        Assert.InRange(cursor, source, source + 0x10000);
        uint start = cursor >= source + 0x8000 ? source + 0x8000 : source;
        int first = checked((int)(start - 0x20DFAB8u));
        int end = checked((int)(cursor - 0x20DFAB8u));
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= first && item.Command.Packet.ByteOffset < end).Select(item => item.Command).ToArray();
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}

