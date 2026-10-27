using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect83PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<byte> rootPhases = [];
    private readonly HashSet<byte> mainPhases = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly List<object> observations = [];
    private bool relocated;
    private bool decodedModel;
    private bool commonBody;
    private bool detached;
    private bool contactHandshake;
    private bool spiralPacketsDelivered;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect83State state = current.NativeEffectMemory!.Effect83!;
        state.Validate();
        Assert.Equal((byte)138, current.NativeGraphics.Models[4].ModelId4);
        Assert.Equal((byte)139, current.NativeGraphics.Models[3].ModelId4);
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Equal(Pools.Length, effect.Pools.Length);
            Assert.All(state.BankStorage, bank => Assert.Null(bank));
            Assert.NotNull(state.NodeData);
            Assert.Equal(NodeSourceLength, state.NodeData!.Length);
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
                        Assert.InRange(phase, (byte)0, (byte)10);
                        if (rootPhases.Add(phase) && phase is 3 or 6 or 7 or 8 or 9)
                        {
                            Mark("effect83-root-phase-" + phase, current, cold);
                        }
                    }
                    if (task.Callback == 0x797A90)
                    {
                        byte phase = task.ReadByte(0x29);
                        Assert.InRange(phase, (byte)0, (byte)4);
                        Assert.Equal((byte)0, task.ReadByte(0x2D));
                        if (mainPhases.Add(phase) && phase is 1 or 2 or 4)
                        {
                            Mark("effect83-main-phase-" + phase, current, cold);
                        }
                        NativeBattleModulePoolTask? previous = prior.NativeGraphics!.OriginalEffect?.Pools.SelectMany(pool => pool.Slots).FirstOrDefault(item => item.Callback == 0x797A90);
                        if (phase == 2 && previous?.ReadByte(0x29) == 1 && !contactHandshake)
                        {
                            Assert.Equal((ushort)1, prior.NativeEffectMemory!.Effect83!.ReadWord(TargetAddress + 0x48));
                            contactHandshake = true;
                            Mark("effect83-contact-handshake", current, cold);
                        }
                    }
                }
            }
            if (effect.Pools[0].Slots.Any(task => task.Callback == RootCallback && task.ReadWord(0x5C) != 0))
            {
                Assert.Equal(ViewAddress, state.ReadDword(0x25CA080));
                Assert.Equal(ViewAddress, state.ReadDword(0x25CFA40));
                Assert.NotNull(current.NativeEffectMemory.SharedEffectView2793E58);
            }
            if (state.ReadDword(NodeSourceAddress) == 1 && !relocated)
            {
                relocated = true;
                Assert.Equal(0u, state.ReadDword(NodeSourceAddress + 0x84 + 6 * 4));
                Mark("effect83-current-node-relocated", current, cold);
            }
            if (state.OwnedModels.Values.Any(model => model.DecodedPose is not null) && !decodedModel)
            {
                decodedModel = true;
                Assert.All(state.OwnedModels.Values, model => Assert.Equal(ModelAddress, model.ResourceAddress));
                Mark("effect83-owned-model-decoded", current, cold);
            }
            ImmutableHashSet<NativeBattlePrimitiveReference> prefix = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
            if (!prefix.IsEmpty && !commonBody)
            {
                commonBody = true;
                Mark("effect83-current-common-prefix", current, cold);
            }
            NativeBattleLogicalCommand[] commands = EffectCommands(prior, current);
            if (effect.Pools[5].Slots.Any(task => task.Callback == 0x79D9B0 && (task.ReadByte(0x26) & 8) != 0))
            {
                NativeBattleLogicalCommand[] privateCommands = commands.Where(command => command.Packet.Bank == "20DFAB8").ToArray();
                if (privateCommands.Length != 0)
                {
                    NativeBattleEffectArena arena = current.NativeEffectMemory.Arena!;
                    int end = checked((int)(state.ReadDword(0x25C9F74) - 0x20DFAB8u));
                    int linkedEnd = privateCommands.Max(command => command.Packet.ByteOffset + checked((1 + (int)(System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(command.Packet.ByteOffset, 4)) >> 24)) * 4));
                    Assert.Equal(end, linkedEnd);
                    if (!spiralPacketsDelivered)
                    {
                        spiralPacketsDelivered = true;
                        Mark("effect83-spiral-packets-delivered", current, cold);
                    }
                }
            }
            foreach (NativeBattleLogicalCommand command in commands)
            {
                opcodes.Add(command.Opcode);
                if (command.Packet.Bank == "20DFAB8")
                {
                    int initializer = checked((int)(state.ReadDword(InitializerGlobal) - 0x20DFAB8u));
                    int bank = (command.Packet.ByteOffset - initializer) / 0x8000;
                    Assert.InRange(bank, 0, 1);
                    if (packetBanks.Add(bank))
                    {
                        Mark("effect83-packet-bank-" + bank, current, cold);
                    }
                }
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.Empty(effect.Pools);
            Assert.All(state.BankStorage, bank => Assert.NotNull(bank));
            detached = true;
            Mark("effect83-physical-banks-detached", current, cold);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, RootPhases = rootPhases.Order().ToArray(), TargetPhase = state.ReadWord(TargetAddress + 0x40), TargetAge = state.ReadWord(TargetAddress + 0x46), ContactFlag = state.ReadWord(TargetAddress + 0x48), BodyFlag = state.ReadWord(TargetAddress + 0x4C), EmitterCursor = state.ReadWord(0x25CA850), GeometryCursor = state.ReadWord(0x25CFA38), EmitterPass = state.ReadWord(0x25CFA3A), GeometryPass = state.ReadWord(0x25CFA28), Random = current.NativeCrtRandom, Relocated = relocated, OwnedModel = decodedModel });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(relocated);
        Assert.True(decodedModel);
        Assert.True(commonBody);
        Assert.True(contactHandshake);
        Assert.True(spiralPacketsDelivered);
        Assert.True(detached);
        Assert.Contains((byte)8, rootPhases);
        Assert.Contains((byte)4, mainPhases);
        Assert.Contains(RootCallback, callbacks);
        Assert.Contains(0x797ED0u, callbacks);
        Assert.Contains(0x79E690u, callbacks);
        Assert.NotEmpty(opcodes);
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order().ToArray());
        Assert.NotNull(initialRandom);
        Assert.NotEqual(initialRandom, lastRandom);
        File.WriteAllText(Path.Combine(output, "effect83-observations.json"), JsonSerializer.Serialize(new { Scenario = "Declared aliveparty0 renderer and lifecycle; ordinary AI allocated-dead-party admission is excluded", Markers = markers.Order(), RootPhases = rootPhases.Order(), MainPhases = mainPhases.Order(), Callbacks = callbacks.Order(), Opcodes = opcodes.Order(), PacketBanks = packetBanks.Order(), Relocated = relocated, OwnedModel = decodedModel, CommonPrefix = commonBody, ContactHandshake = contactHandshake, Detached = detached, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot prior, BattleSnapshot current)
    {
        ImmutableHashSet<NativeBattlePrimitiveReference> prefix = NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current);
        return EffectCommands(current).Concat(current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => prefix.Contains(item.Command.Packet)).Select(item => item.Command)).Distinct().ToArray();
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        NativeBattleEffect83State state = current.NativeEffectMemory!.Effect83!;
        uint source = state.ReadDword(InitializerGlobal);
        uint cursor = state.ReadDword(0x25C9F74);
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
