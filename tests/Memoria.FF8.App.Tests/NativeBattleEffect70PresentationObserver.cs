using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect70PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<short> loadedFiles = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private bool sparks;
    private bool rings;
    private bool secondaryDetached;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;

    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect70State state = current.NativeEffectMemory!.Effect70!;
        state.Validate();
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        CurrentCommands = EffectCommands(current);
        foreach (NativeBattleLogicalCommand command in CurrentCommands)
        {
            opcodes.Add(command.Opcode);
            int bank = (command.Packet.ByteOffset - FirstPacketOffset) / 0x10000;
            Assert.InRange(bank, 0, 2);
            packetBanks.Add(bank);
        }
        NativeBattleModulePoolTask? before = ReadMain(prior);
        NativeBattleModulePoolTask? main = ReadMain(current);
        if (before is not null && (before.Flags & 1) != 0 && before.ReadWord(12) == 252 && main?.ReadWord(12) == 253)
        {
            Contacts++;
            Mark("effect70-contact252", current, cold);
        }
        for (int index = 0; index < current.NativeGraphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask text = current.NativeGraphics.Effects.Slots[index];
            if ((text.Flags0 & 1) != 0 && text.Kind == NativeBattleEffectKind.DamageText5069B0 && text.ModelIndex == 0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.InRange(effect.Pools.Length, 1, 3);
            Assert.Null(state.RootBankStorage);
            if (effect.Pools.Length == 1)
            {
                Mark("effect70-root-before-common-banks", current, cold);
            }
            for (int index = 0; index < effect.Pools.Length; index++)
            {
                NativeBattleCureFamilyPool expected = Pools[index];
                NativeBattleEffectTaskPool pool = effect.Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal((int)expected.Capacity, pool.Slots.Length);
                callbacks.UnionWith(pool.Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
                if (index != 0)
                {
                    Assert.All(current.NativeEffectMemory.Arena!.ReadSnapshot(checked((int)(pool.BankAddress - ArenaAddress)), pool.Stride * pool.Slots.Length).Known, value => Assert.False(value));
                }
            }
            if (main is not null && (main.Flags & 1) != 0)
            {
                int age = main.ReadWord(12);
                Assert.Equal((ushort)1, main.ReadWord(14));
                Assert.Equal(0x1D972C0u, main.ReadDword(0x14));
                if (age is 1 or 50 or 80 or 94 or 196 or 213 or 214 or 252 or 262)
                {
                    Mark("effect70-main-age-" + age, current, cold);
                }
                NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                if (!sparks && Enumerable.Range(0, 128).Any(index => arena.Read(SparkOffset + index * 16 + 15, 1)[0] != 0))
                {
                    sparks = true;
                    Mark("effect70-current-sparks", current, cold);
                }
                if (!rings && Enumerable.Range(0, 64).Any(index => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(RingOffset + index * 16, 2)) != 0))
                {
                    rings = true;
                    Mark("effect70-current-rings", current, cold);
                }
                if (age >= 214)
                {
                    Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(IoOffset + 8, 2)));
                    Assert.Equal((short)360, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(IoOffset + 10, 2)));
                    Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(IoOffset + 16, 2)));
                    Assert.Equal((short)361, BinaryPrimitives.ReadInt16LittleEndian(arena.Read(IoOffset + 18, 2)));
                    foreach (short file in new short[] { 360, 361 })
                    {
                        NativeBattleEffectFileAsset source = effect.Binding.Effect70Resources!.ReadFile(file);
                        if (!loadedFiles.Contains(file) && arena.Read(StreamOffset, source.Data.Length).AsSpan().SequenceEqual(source.Data.AsSpan()))
                        {
                            loadedFiles.Add(file);
                            Mark("effect70-current-file-" + file, current, cold);
                        }
                    }
                }
                if (age >= 262 && effect.Pools[2].Head is null && !secondaryDetached)
                {
                    secondaryDetached = true;
                    Mark("effect70-secondary-links-detached", current, cold);
                }
            }
        }
        else
        {
            Assert.Empty(effect.Pools);
            Assert.NotNull(state.RootBankStorage);
            Mark("effect70-physical-banks-released", current, cold);
        }
        observations.Add(new { current.Frame, Callback = current.NativeGraphics.Callback, Age = main?.ReadWord(12), Pools = effect.Pools.Length, PrimaryCursor = state.ReadDword(CursorGlobal), SecondaryCursor = state.ReadDword(SecondaryCursorGlobal), Depth = state.ReadDword(DepthGlobal), current.NativeCrtRandom, Contacts, ContactGlyphs, Packets = CurrentCommands.Length });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.True(sparks);
        Assert.True(rings);
        Assert.Contains((short)360, loadedFiles);
        Assert.Contains((short)361, loadedFiles);
        Assert.Contains(RootCallback, callbacks);
        Assert.Contains(MainCallback, callbacks);
        Assert.Contains(0x6E0100u, callbacks);
        Assert.Contains(0x6DFF00u, callbacks);
        Assert.True(packetBanks.Count >= 2);
        Assert.NotEmpty(opcodes);
        Assert.NotEqual(initialRandom, lastRandom);
        Assert.Contains("effect70-main-age-80", markers);
        Assert.Contains("effect70-main-age-214", markers);
        Assert.Contains("effect70-main-age-252", markers);
        Assert.Contains("effect70-main-age-262", markers);
        Assert.Contains("effect70-physical-banks-released", markers);
        Assert.Equal(0u, completed.NativeEffectMemory!.Effect70!.ReadDword(DepthGlobal));
        foreach (uint sound in completed.Definition.NativeEffectCatalog[69].Effect70Resources!.Sounds.Values)
        {
            Assert.Contains(completed.Events, item => item.NativeModuleSound?.Sound == sound);
        }
        File.WriteAllText(Path.Combine(output, "effect70-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), LoadedFiles = loadedFiles.Order(), Opcodes = opcodes.Order(), Contacts, ContactGlyphs, Sparks = sparks, Rings = rings, SecondaryLinksDetached = secondaryDetached, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    internal static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        NativeBattleEffect70State? state = current.NativeEffectMemory?.Effect70;
        if (state is null || current.NativeGraphics?.OriginalEffect?.Binding.Effect != Effect)
        {
            return [];
        }
        uint primary = state.ReadDword(CursorGlobal);
        uint secondary = state.ReadDword(SecondaryCursorGlobal);
        bool Contains(int offset, uint cursor)
        {
            if (cursor <= ArenaAddress + FirstPacketOffset)
            {
                return false;
            }
            int end = checked((int)(cursor - ArenaAddress));
            int start = FirstPacketOffset + ((end - FirstPacketOffset - 1) / 0x10000) * 0x10000;
            return offset >= start && offset < end;
        }
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && (Contains(command.Packet.ByteOffset, primary) || Contains(command.Packet.ByteOffset, secondary))).ToArray();
    }

    private static NativeBattleModulePoolTask? ReadMain(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect?.Binding.Effect == Effect ? snapshot.NativeGraphics.OriginalEffect.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(task => task.Callback == MainCallback) : null;

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
