using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect303PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> programmeFrames = [];
    private readonly HashSet<int> childFrames = [];
    private readonly List<object> observations = [];
    private bool fragments;
    private bool particles;
    private bool sprites;
    private bool bodyCommon;
    private bool bodyFx;
    private bool queued;
    private bool detached;
    private bool activated;
    private int? vibration;

    private int capturedPrimitives;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        capturedPrimitives += NativeBattleRecovery32CaptureAssertions.AssertFrame(current, 303);
        NativeBattleOriginalEffectState? before = prior.NativeGraphics!.OriginalEffect;
        NativeBattleOriginalEffectState? effect = current.NativeGraphics!.OriginalEffect;
        if (effect?.Binding.Effect != 303 || effect.ReturnedDescriptor is null)
        {
            return;
        }
        NativeBattleEffect303State state = current.NativeEffectMemory!.Effect303!;
        if (effect.ReturnedDescriptor is > 0)
        {
            Assert.Null(state.RootBankStorage);
            Assert.Equal(0x1D972C0u + 3 * 156u, state.ReadDword(NativeBattleEffect303Profile.CasterGlobal));
            Assert.Equal(0x1D972C0u + 4 * 156u, state.ReadDword(NativeBattleEffect303Profile.TargetGlobal));
            Assert.Equal(NativeBattleEffect303Profile.ArenaAddress, state.ReadDword(NativeBattleEffect303Profile.ArenaGlobal));
            foreach ((NativeBattleEffectTaskPool pool, int index) in effect.Pools.Select((pool, index) => (pool, index)))
            {
                NativeBattleCureFamilyPool expected = NativeBattleEffect303Profile.Pools[index];
                Assert.Equal(expected.Descriptor, pool.DescriptorAddress);
                Assert.Equal(expected.Bank, pool.BankAddress);
                Assert.Equal(expected.Stride, pool.Stride);
                Assert.Equal(expected.Capacity, pool.Slots.Length);
                if (index == 2)
                {
                    foreach (NativeBattleModulePoolTask retained in pool.Slots.Where(task => task.Callback == 0x66C220))
                    {
                        NativeBattleGfAnimationWorkspace cache = new(retained.Payload.AsSpan().Slice(20, 280).ToArray().ToImmutableArray());
                        Assert.Equal(NativeBattleEffect303Profile.ChildProgrammeAddress, cache.ResourceAddress);
                        childFrames.Add(cache.Frame);
                    }
                }
                foreach (NativeBattleModulePoolTask task in pool.Slots.Where(task => (task.Flags & 1) != 0))
                {
                    callbacks.Add(task.Callback);
                    if (index == 1)
                    {
                        int age = unchecked((short)task.ReadWord(12));
                        vibration = unchecked((int)task.ReadDword(16));
                        fragments |= Enumerable.Range(0, 128).Any(row => task.ReadWord(0x20 + row * 76 + 6) != 0);
                        NativeBattleGfAnimationWorkspace cache = new(task.Payload.AsSpan().Slice(0x2620 - 12, 416).ToArray().ToImmutableArray());
                        Assert.Equal(NativeBattleEffect303Profile.MainProgrammeAddress, cache.ResourceAddress);
                        programmeFrames.Add(cache.Frame);
                        if (age >= 38)
                        {
                            Assert.False(current.NativeVibrationProcess!.IsPlaying(vibration.Value));
                        }
                        if (age is 1 or 2 or 7 or 22 or 24 or 25 or 37 or 38 or 40 or 41)
                        {
                            Mark("effect303-main-" + age, current, cold);
                        }
                        if (age is >= 25 and <= 40)
                        {
                            bodyCommon |= !NativeBattleAquaPresentationTests.AquaPacketReferences(prior, current).IsEmpty;
                            bodyFx |= EffectCommands(current).Any(command => command.Opcode == 0xE2);
                            if (bodyCommon && bodyFx)
                            {
                                Mark("effect303-body-dual-cursors", current, cold);
                            }
                        }
                    }
                    particles |= index == 3;
                    sprites |= index == 4;
                }
            }
            int cursor = checked((int)(state.ReadDword(NativeBattleEffect303Profile.CursorGlobal) - NativeBattleEffect303Profile.ArenaAddress));
            int bank = cursor >= NativeBattleEffect303Profile.SecondPacketOffset ? 1 : 0;
            if (banks.Add(bank))
            {
                Mark("effect303-packet-bank-" + bank, current, cold);
            }
            if (current.NativeEffectMemory.EffectIo?.BaseOffset == NativeBattleEffect303Profile.IoOffset)
            {
                byte[] io = current.NativeEffectMemory.Arena!.Read(NativeBattleEffect303Profile.IoOffset, 16);
                if (io[3] != 0)
                {
                    queued = true;
                    Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(8)));
                    Assert.Equal((ushort)360, BinaryPrimitives.ReadUInt16LittleEndian(io.AsSpan(10)));
                    Mark("effect303-file360-type1", current, cold);
                }
            }
        }
        if (before?.ReturnedDescriptor is > 0 && effect.ReturnedDescriptor == 0)
        {
            detached = true;
            Assert.NotNull(state.RootBankStorage);
            Mark("effect303-root-detached", current, cold);
        }
        NativeBattleParticipantSnapshot target = current.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 4).NativeParticipant!;
        if (!target.Hidden && !target.Suspended && !target.Untargetable)
        {
            activated = true;
            Assert.Single(current.Events, item => item.Kind == "Activated" && item.ActorId == current.NativeGraphics.ActorIds[4]);
            Mark("effect303-native-recipient-activation", current, cold);
        }
        else
        {
            Assert.False(activated);
        }
        observations.Add(new { current.Frame, current.NativeGraphics.Callback, effect.ReturnedDescriptor, Cursor = state.ReadDword(NativeBattleEffect303Profile.CursorGlobal), target.Hidden, target.Suspended, target.Untargetable, Activated = activated });
    }

    public bool ActivationCompleted(BattleSnapshot current)
    {
        NativeBattleDescriptorState descriptors = current.NativeGraphics!.Descriptors;
        return activated && !descriptors.Queue.Any(index => descriptors.Packets[index].Command == 113) && !descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Callback502F30);
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(capturedPrimitives > 0, "The complete standalone route did not observe owned moving geometry.");
        NativeBattleRecovery32CaptureAssertions.AssertFrame(completed, 303);
        Assert.True(fragments);
        Assert.True(particles);
        Assert.True(sprites);
        Assert.True(bodyCommon);
        Assert.True(bodyFx);
        Assert.True(queued);
        Assert.True(detached);
        Assert.True(ActivationCompleted(completed));
        Assert.True(vibration is > 0);
        Assert.False(completed.NativeVibrationProcess!.IsPlaying(vibration.Value));
        Assert.Equal(new[] { 0, 1 }, banks.Order().ToArray());
        Assert.Contains(22, programmeFrames);
        Assert.Contains(16, childFrames);
        foreach (uint callback in new uint[] { 0x66A920, 0x66AAF0, 0x66C220, 0x66C100, 0x66C080 })
        {
            Assert.Contains(callback, callbacks);
        }
        foreach (string marker in new[] { "effect303-main-1", "effect303-main-25", "effect303-main-40", "effect303-main-41", "effect303-file360-type1", "effect303-body-dual-cursors", "effect303-root-detached", "effect303-native-recipient-activation" })
        {
            Assert.Contains(marker, markers);
        }
        File.WriteAllText(Path.Combine(output, "effect303-observations.json"), JsonSerializer.Serialize(new { MainFrames = programmeFrames.Order(), ChildFrames = childFrames.Order(), Callbacks = callbacks.Order(), Markers = markers.Order(), Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] EffectCommands(BattleSnapshot current)
    {
        uint cursor = current.NativeEffectMemory!.Effect303!.ReadDword(NativeBattleEffect303Profile.CursorGlobal);
        if (cursor == 0)
        {
            return [];
        }
        int end = checked((int)(cursor - NativeBattleEffect303Profile.ArenaAddress));
        int start = end >= NativeBattleEffect303Profile.SecondPacketOffset ? NativeBattleEffect303Profile.SecondPacketOffset : NativeBattleEffect303Profile.FirstPacketOffset;
        Assert.InRange(end, start, start + NativeBattleEffect303Profile.PacketBankBytes);
        return current.NativeGraphics!.PrimitiveFrame!.LastCommands.Where(item => item.Command.Packet.Bank == "20DFAB8" && item.Command.Packet.Buffer == 0 && item.Command.Packet.ByteOffset >= start && item.Command.Packet.ByteOffset < end).Select(item => item.Command).ToArray();
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
