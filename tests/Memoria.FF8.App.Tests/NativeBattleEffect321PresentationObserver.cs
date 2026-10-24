using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect321Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect321PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> recordTypes = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool? cameraDecision;
    private bool disabledCamera;
    private bool vibrationStarted;
    private bool vibrationStopped;
    private bool capturedActor;
    private bool orbit;
    private bool restoredActor;
    private bool positionalSound;
    private bool mainRetired;
    private bool geometry;
    private bool drained;
    private bool cleanupQueued;
    private bool cleanupCompleted;
    private uint? cleanupAddress;
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
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect321State state = current.NativeEffectMemory!.Effect321!;
        NativeBattleEffect321Resources resources = effect.Binding.Effect321Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        callbacks.UnionWith(tasks.Values.Select(task => task.Callback));
        Assert.Equal((byte)100, graphics.Models[3].ModelId4);
        Assert.Equal(11, graphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool currentTrack = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            track |= currentTrack;
            if (currentTrack)
            {
                view |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            }
            if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
            {
                cameraDecision = decision;
                if (!decision && current.NativeCamera.Variables[0] == -1 && current.NativeCamera.Scratch[0] == -1 && current.NativeCamera.ControlCursor is null && current.NativeCamera.Order.IsEmpty)
                {
                    disabledCamera = true;
                }
            }
            Mark("effect321-camera-current8-24-owners", current, cold);
        }
        if (!vibrationStarted && main is not null && age >= 1)
        {
            int handle = unchecked((int)state.ReadDword(VibrationGlobal));
            NativeVibrationSlot slot = current.NativeVibrationProcess!.Slots[handle & 3];
            Assert.Equal(handle, slot.Generation * 16 | slot.Id);
            Assert.Equal((byte)128, slot.Priority);
            NativeVibrationBank bank = resources.VibrationBank;
            int offset = bank.Offsets[1];
            Assert.Equal((int)BinaryPrimitives.ReadUInt16LittleEndian(bank.Bytes.AsSpan(offset, 2)), slot.A.Length);
            Assert.Equal((int)BinaryPrimitives.ReadUInt16LittleEndian(bank.Bytes.AsSpan(offset + 2, 2)), slot.B.Length);
            Assert.True(current.NativeVibrationProcess.IsPlaying(handle));
            vibrationStarted = true;
            Mark("effect321-returned-vibration-handle", current, cold);
        }
        if (oldAge == 92)
        {
            positionalSound = true;
            Mark("effect321-positional-sound92-current-target-center", current, cold);
        }
        if (oldAge == 97)
        {
            Assert.False(current.NativeVibrationProcess!.IsPlaying(unchecked((int)state.ReadDword(VibrationGlobal))));
            vibrationStopped = true;
            Mark("effect321-vibration97-query-stop", current, cold);
        }
        if (before is not null && main is null)
        {
            Assert.Equal(100, oldAge);
            mainRetired = true;
            Mark("effect321-main101-retired", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(item => item.Value.Callback == 0x5985C0))
        {
            int actor = unchecked((short)task.ReadWord(14));
            Assert.InRange(actor, 0, 6);
            uint saved = SavedActorsAddress + (uint)(actor * SavedActorStride);
            NativeBattleEffectArena model = NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]);
            if (!previous.ContainsKey(address))
            {
                NativeBattleEffectArena entering = NativeBattleRaldo19ModelMemory.Capture(prior.NativeGraphics!.Models[actor]);
                Assert.Equal((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(entering.Read(0, 2)) & 0x1020), state.ReadWord(saved));
                EqualSnapshot(entering, 12, state.ReadView(saved + 4, 8), 0, 8);
                EqualSnapshot(entering, 28, state.ReadView(saved + 12, 8), 0, 8);
                capturedActor = true;
                Mark("effect321-target-current-packed-capture", current, cold);
            }
            int targetAge = task.ReadWord(12);
            if (targetAge is >= 1 and < 66)
            {
                Assert.Equal(0x1020, graphics.Models[actor].Flags0 & 0x1020);
                orbit |= !model.Read(28, 6).SequenceEqual(state.Read(saved + 12, 6));
            }
            if (targetAge is 1 or 12 or 54 or 66 or 67)
            {
                Mark("effect321-before-target-age-" + targetAge, current, cold);
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous.Where(item => item.Value.Callback == 0x5985C0))
        {
            if (tasks.ContainsKey(address))
            {
                continue;
            }
            Assert.Equal((ushort)67, task.ReadWord(12));
            int actor = unchecked((short)task.ReadWord(14));
            uint saved = SavedActorsAddress + (uint)(actor * SavedActorStride);
            NativeBattleEffectArena model = NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]);
            EqualSnapshot(model, 12, state.ReadView(saved + 4, 8), 0, 8);
            EqualSnapshot(model, 28, state.ReadView(saved + 12, 8), 0, 8);
            Assert.Equal((uint)state.ReadWord(saved), (uint)(graphics.Models[actor].Flags0 & 0x1020));
            restoredActor = true;
            Contacts++;
            Mark("effect321-restored-target-contact67-to68", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(item => item.Value.Callback == CleanupCallback))
        {
            cleanupAddress = address;
            Mark("effect321-cleanup-active", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous.Where(item => item.Value.Callback == CleanupCallback && item.Value.ReadWord(12) == 1))
        {
            cleanupAddress = address;
            cleanupQueued = true;
            Mark("effect321-A3-first-packet-bank-completion-request", current, cold);
        }
        for (int index = 0; index < RecordCount; index++)
        {
            uint address = RecordsAddress + (uint)(index * RecordStride);
            uint marker = state.ReadDword(address);
            if (marker != 0)
            {
                Assert.Contains(marker, new uint[] { 1, 2, 4 });
                recordTypes.Add(marker);
                Assert.InRange(state.ReadWord(address + 4), 0, 17);
                if (markers.Add("effect321-record-type-" + marker))
                {
                    cold("effect321-record-type-" + marker, current);
                }
            }
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        uint cursor = state.ReadDword(CursorGlobal);
        uint start = cursor >= SecondPacketAddress ? SecondPacketAddress : FirstPacketAddress;
        if (cursor >= start && cursor <= start + PacketLength)
        {
            string bank = start.ToString("X");
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == bank && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= 0 && command.Packet.ByteOffset < cursor - start).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (CurrentCommands.Length > 0)
            {
                banks.Add(start == FirstPacketAddress ? 0 : 1);
                geometry |= CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length == 4);
                Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
            }
        }
        if (age is 1 or 24 or 25 or 92 or 97 or 100)
        {
            Mark("effect321-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
            if (cleanupAddress is uint address)
            {
                int offset = checked((int)(address - RootBank)) + 14;
                Assert.NotEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(state.ReadBank(RootBank).Read(offset, 2)));
                cleanupCompleted = true;
            }
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Cursor = cursor, Commands = CurrentCommands.Length, RecordTypes = recordTypes.Order(), Contacts, ContactGlyphs });
    }

    public void Publish(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect321-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Callbacks = callbacks.Order(), RecordTypes = recordTypes.Order(), Camera = camera, CameraTrack = track, CameraViewChanged = view, CameraDecision = cameraDecision, DeclinedCamera = disabledCamera, VibrationStarted = vibrationStarted, VibrationStopped = vibrationStopped, CapturedActor = capturedActor, Orbit = orbit, RestoredActor = restoredActor, PositionalSound92 = positionalSound, Geometry = geometry, MainRetired = mainRetired, Drained = drained, CleanupQueued = cleanupQueued, CleanupCompleted = cleanupCompleted, Contacts, ContactGlyphs, Observations = observations }));
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track);
            Assert.True(view);
        }
        else
        {
            Assert.True(disabledCamera && !track);
        }
        Assert.True(vibrationStarted);
        Assert.True(vibrationStopped);
        Assert.True(capturedActor);
        Assert.True(orbit);
        Assert.True(restoredActor);
        Assert.True(positionalSound);
        Assert.True(mainRetired);
        Assert.True(geometry);
        Assert.True(drained);
        Assert.True(cleanupQueued);
        Assert.True(cleanupCompleted);
        Assert.Equal(new uint[] { 1, 2, 4 }, recordTypes.Order());
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        foreach (uint callback in new uint[] { 0x597E30, 0x597F90, 0x598230, 0x598570, 0x5985C0, 0x5988B0, 0x598B90 })
        {
            Assert.Contains(callback, callbacks);
        }
        Assert.All(completed.NativeEffectMemory!.Effect321!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static void EqualSnapshot(NativeBattleEffectArena left, int leftOffset, NativeBattleEffectArena right, int rightOffset, int length)
    {
        var a = left.ReadSnapshot(leftOffset, length);
        var b = right.ReadSnapshot(rightOffset, length);
        Assert.Equal(a.Known, b.Known);
        for (int index = 0; index < length; index++)
        {
            if (a.Known[index])
            {
                Assert.Equal(a.Data[index], b.Data[index]);
            }
        }
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
            int? index = pool.Head;
            HashSet<int> visited = [];
            while (index is int slot)
            {
                Assert.True(visited.Add(slot));
                result.Add(pool.BankAddress + (uint)(slot * pool.Stride), pool.Slots[slot]);
                index = pool.Slots[slot].Next;
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
