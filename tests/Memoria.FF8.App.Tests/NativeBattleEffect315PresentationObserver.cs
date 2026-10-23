using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect315PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> mainAges = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> particleTypes = [];
    private readonly HashSet<byte> opcodes = [];
    private readonly List<object> observations = [];
    private ushort? savedDefault;
    private int? vibration;
    private bool bodyCommon;
    private bool restored;
    private bool detached;
    private bool activated;
    private bool colorChanged;
    private bool contactProjection;
    private NativeCrtRandomState? initialRandom;
    private NativeCrtRandomState? lastRandom;
    private uint? originalColor;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != Effect || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect315State state = current.NativeEffectMemory!.Effect315!;
        NativeBattleGraphicsModel caster = current.NativeGraphics.Models[3];
        Assert.Equal((byte)91, caster.ModelId4);
        Assert.Equal((byte)50, current.NativeGraphics.Models[4].ModelId4);
        initialRandom ??= prior.NativeCrtRandom;
        lastRandom = current.NativeCrtRandom;
        originalColor ??= prior.NativeGraphics!.Models[3].Color40;
        colorChanged |= caster.Color40 != originalColor;
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Null(state.RootBankStorage);
            Assert.Null(state.ChildBankStorage);
            Assert.Equal(3u, state.ReadDword(CasterGlobal));
            Assert.Equal(3u, state.ReadDword(TargetGlobal));
            Assert.Equal(ArenaAddress, state.ReadDword(ArenaGlobal));
            Assert.Equal(2, effect.Pools.Length);
            foreach ((NativeBattleEffectTaskPool pool, int index) in effect.Pools.Select((pool, index) => (pool, index)))
            {
                NativeBattleCureFamilyPool expected = Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => task.Callback != 0))
                {
                    callbacks.Add(task.Callback);
                    if (task.Callback != 0x598E70)
                    {
                        continue;
                    }
                    int age = unchecked((short)task.ReadWord(0xC));
                    mainAges.Add(age);
                    if (age > 0)
                    {
                        savedDefault ??= task.ReadWord(0xE);
                        Assert.InRange(savedDefault.Value, (ushort)0, (ushort)255);
                        Assert.Equal(savedDefault.Value, task.ReadWord(0xE));
                        vibration = unchecked((int)state.ReadDword(VibrationGlobal));
                    }
                    if (age is > 0 and <= 112 && (task.Flags & 1) != 0)
                    {
                        Assert.NotEqual(0u, caster.Flags8!.Value & 0x8000);
                    }
                    if (age is 1 or 14 or 31 or 33 or 83 or 96 or 113 or 127 or 160)
                    {
                        Mark("effect315-main-" + age, current, cold);
                    }
                }
            }
            NativeBattleOriginalEffectState? beforeEffect = prior.NativeGraphics!.OriginalEffect;
            if (!restored && beforeEffect?.Binding.Effect == Effect && beforeEffect.Pools.Length == 2)
            {
                foreach ((NativeBattleModulePoolTask task, int slot) in beforeEffect.Pools[1].Slots.Select((task, slot) => (task, slot)))
                {
                    NativeBattleModulePoolTask next = effect.Pools[1].Slots[slot];
                    if ((task.Flags & 1) != 0 && task.Callback == 0x598E70 && unchecked((short)task.ReadWord(0xC)) == 112 && unchecked((short)next.ReadWord(0xC)) == 113)
                    {
                        NativeBattleActionPacketsState packets = current.NativeActionPackets!;
                        int first = packets.Headers[effect.Parameters!.HeaderIndex7C].FirstContactIndex!.Value;
                        NativeBattleContactHalf half = packets.ReadContact(first).Primary;
                        Assert.Equal((byte)3, half.ActorIndex);
                        NativeBattleGraphicsModel beforeCaster = prior.NativeGraphics.Models[3];
                        Assert.Null(beforeCaster.ReadLinkedModelIndex140());
                        uint battleStatuses = current.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3).NativeStatus!.Battle;
                        NativeBattleContactEffectPlan plan = NativeBattleContactEffects.Plan(half, beforeCaster, current.Definition.NativeGraphics!.Rules, battleStatuses);
                        Assert.True(plan.Loaded);
                        Assert.Equal(plan.ImmediateStatusFlags, caster.Flags8);
                        contactProjection = true;
                        Mark("effect315-self-contact-status-projection", current, cold);
                    }
                    if ((task.Flags & 1) == 0 || task.Callback != 0x598E70 || unchecked((short)task.ReadWord(0xC)) != 160)
                    {
                        continue;
                    }
                    NativeBattleModulePoolTask retired = effect.Pools[1].Slots[slot];
                    if (unchecked((short)retired.ReadWord(0xC)) == 161 && (retired.Flags & 1) == 0)
                    {
                        Assert.Equal(0u, caster.Flags8!.Value & 0x8000);
                        Assert.Equal((byte)task.ReadWord(0xE), caster.SequenceState!.Default2);
                        savedDefault ??= task.ReadWord(0xE);
                        mainAges.Add(161);
                        restored = true;
                        Mark("effect315-main-restored", current, cold);
                    }
                }
            }
            for (int index = 0; index < ParticleCount; index++)
            {
                uint type = state.ReadDword(checked(ParticleAddress + (uint)(index * ParticleStride)));
                if (type != 0)
                {
                    Assert.Equal(0u, type & ~0x77u);
                    if (particleTypes.Add(type))
                    {
                        Mark("effect315-particle-" + type.ToString("X"), current, cold);
                    }
                }
            }
            uint cursor = state.ReadDword(CursorGlobal);
            if (cursor != 0)
            {
                int end = checked((int)(cursor - ArenaAddress));
                int bank = end >= SecondPacketOffset ? 1 : 0;
                Assert.InRange(end, bank == 0 ? 0 : SecondPacketOffset, NativeBattleEffectArena.Length);
                if (banks.Add(bank))
                {
                    Mark("effect315-packet-bank-" + bank, current, cold);
                }
            }
            bodyCommon |= !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current).IsEmpty;
            foreach (NativeBattleLogicalCommand command in EffectCommands(current))
            {
                opcodes.Add(command.Opcode);
            }
        }
        if (prior.NativeGraphics!.OriginalEffect?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            Assert.NotNull(state.RootBankStorage);
            Assert.NotNull(state.ChildBankStorage);
            Assert.Empty(effect.Pools);
            detached = true;
            Mark("effect315-root-detached", current, cold);
        }
        NativeBattleParticipantSnapshot recipient = current.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4).NativeParticipant!;
        if (!recipient.Hidden && !recipient.Suspended && !recipient.Untargetable)
        {
            activated = true;
            Assert.Single(current.Events, item => item.Kind == "Activated" && item.ActorId == current.NativeGraphics.ActorIds[4]);
            Mark("effect315-native-recipient-activation", current, cold);
        }
        else
        {
            Assert.False(activated);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, Cursor = state.ReadDword(CursorGlobal), Vibration = vibration, CasterDefault2 = caster.SequenceState?.Default2, caster.Flags8, caster.Color40, recipient.Hidden, recipient.Suspended, recipient.Untargetable, Random = current.NativeCrtRandom });
    }

    public bool ActivationCompleted(BattleSnapshot current)
    {
        NativeBattleDescriptorState descriptors = current.NativeGraphics!.Descriptors;
        return activated && !descriptors.Queue.Any(index => descriptors.Packets[index].Command == 113) && !descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Callback502F30);
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(restored);
        Assert.True(detached);
        Assert.True(ActivationCompleted(completed));
        Assert.True(colorChanged);
        Assert.True(contactProjection);
        Assert.True(bodyCommon);
        Assert.True(vibration is > 0);
        Assert.False(completed.NativeVibrationProcess!.IsPlaying(vibration.Value));
        Assert.NotNull(initialRandom);
        Assert.NotEqual(initialRandom, lastRandom);
        Assert.Equal(new[] { 0, 1 }, banks.Order().ToArray());
        Assert.NotEmpty(opcodes);
        foreach (uint callback in new uint[] { 0x598E70, 0x599470, 0x599860, 0x599A90, 0x599CC0, 0x59A3E0, 0x59A500, 0x59A670, 0x59A7F0, 0x59AAF0, 0x59AC70, 0x59ADB0, 0x59AEF0, 0x59B000, 0x59B070 })
        {
            Assert.Contains(callback, callbacks);
        }
        foreach (uint type in new uint[] { 1, 2, 4, 0x10, 0x20, 0x40 })
        {
            Assert.Contains(type, particleTypes);
        }
        Assert.Contains(161, mainAges);
        NativeBattleRestorationPresentationTests.Publish(output, "activated-original-pair", completed, null);
        File.WriteAllText(Path.Combine(output, "effect315-observations.json"), JsonSerializer.Serialize(new { MainAges = mainAges.Order(), Callbacks = callbacks.Order(), ParticleTypes = particleTypes.Order(), PacketBanks = banks.Order(), Opcodes = opcodes.Order(), Markers = markers.Order(), BodyCommon = bodyCommon, ContactProjection = contactProjection, Restored = restored, Detached = detached, Activated = activated, InitialRandom = initialRandom, LastRandom = lastRandom, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        uint cursor = current.NativeEffectMemory!.Effect315!.ReadDword(CursorGlobal);
        if (cursor == 0)
        {
            return [];
        }
        int end = checked((int)(cursor - ArenaAddress));
        int start = end >= SecondPacketOffset ? SecondPacketOffset : FirstPacketOffset;
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).Select(item => item.Command).Distinct().ToArray();
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
