using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect313PresentationObserver
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
    private bool capturedActor;
    private bool sequenceWait;
    private readonly HashSet<int> restoredActors = [];
    private readonly HashSet<int> eligibleActors = [];
    private readonly HashSet<int> bornActors = [];
    private bool eligibilityCaptured;
    private readonly Dictionary<int, JsonElement> capacityFrames = [];
    private readonly HashSet<int> verifiedCapacityAges = [];
    private readonly bool verifyCapacity;
    private uint expectedEntrySeed;
    private int expectedPeak;
    private int expectedPeakAge;
    private int maximumPostScanLive;
    private bool entrySeedChecked;
    private bool orbit;
    private bool restoredActor;
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

    public NativeBattleEffect313PresentationObserver(bool requireFreshCapacity = true)
    {
        string? path = Environment.GetEnvironmentVariable(requireFreshCapacity ? "MEMORIA_FF8_EFFECT313_CAPACITY_EXPECTATION" : "MEMORIA_FF8_EFFECT313_FOLLOWING_CAPACITY_EXPECTATION");
        if (requireFreshCapacity && string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidDataException("Select the current source capacity expectation.");
        }
        verifyCapacity = !string.IsNullOrWhiteSpace(path);
        if (verifyCapacity)
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path!));
            JsonElement root = document.RootElement;
            Assert.Equal("PASS_FRESH313_CAPACITY", root.GetProperty("status").GetString());
            expectedEntrySeed = root.GetProperty("start_seed").GetUInt32();
            if (requireFreshCapacity)
            {
                Assert.Equal(1u, expectedEntrySeed);
            }
            expectedPeak = root.GetProperty("early_peak").GetInt32();
            expectedPeakAge = root.GetProperty("early_peak_main_age").GetInt32();
            Assert.True(expectedPeak <= 100);
            foreach (JsonElement row in root.GetProperty("early_by_callback").EnumerateArray())
            {
                capacityFrames.Add(row.GetProperty("main_age").GetInt32(), row.Clone());
            }
        }
    }

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect313State state = current.NativeEffectMemory!.Effect313!;
        NativeBattleEffect313Resources resources = effect.Binding.Effect313Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        callbacks.UnionWith(tasks.Values.Select(task => task.Callback));
        if (oldAge == 122)
        {
            eligibleActors.UnionWith(prior.NativeGraphics!.Models.Take(3).Where(model => (model.Flags0 & 2) != 0).Select(model => model.Index));
            eligibilityCaptured = true;
            Mark("effect313-current-party-flags-at-birth122", current, cold);
        }

        if (!entrySeedChecked && main is not null && age <= 1)
        {
            if (verifyCapacity)
            {
                Assert.Equal(expectedEntrySeed, current.NativeCrtRandom!.Seed);
            }
            entrySeedChecked = true;
            Mark("effect313-actual-entry-CRT-seed", current, cold);
        }
        int processedAge = before is not null ? oldAge : main is not null && age > 0 ? age - 1 : -1;
        if (verifyCapacity && processedAge is >= 0 and <= 98 && verifiedCapacityAges.Add(processedAge))
        {
            JsonElement expected = capacityFrames[processedAge];
            Assert.Equal(expected.GetProperty("seed").GetUInt32(), current.NativeCrtRandom!.Seed);
            NativeBattleModulePoolTask[] children = tasks.Where(pair => pair.Key >= ChildBank && pair.Key < ChildBank + 3600).Select(pair => pair.Value).ToArray();
            Assert.Equal(expected.GetProperty("live").GetInt32(), children.Length);
            Dictionary<string, int> actual = children.GroupBy(task => CallbackName(task.Callback)).ToDictionary(group => group.Key, group => group.Count());
            Dictionary<string, int> counts = expected.GetProperty("by_callback").EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetInt32());
            Assert.Equal(counts.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
            maximumPostScanLive = Math.Max(maximumPostScanLive, children.Length);
            if (processedAge == 98)
            {
                Mark("effect313-actual-CRT-and-capacity-through98", current, cold);
            }
        }

        Assert.Equal((byte)17, graphics.Models[3].ModelId4);
        Assert.Equal(36, graphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
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
            Mark("effect313-camera-current8-24-owners", current, cold);
        }
        if (before is not null && main is null)
        {
            Assert.Equal(190, oldAge);
            mainRetired = true;
            Mark("effect313-main191-retired", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(item => item.Value.Callback == 0x59C4F0))
        {
            int actor = unchecked((short)task.ReadWord(14));
            Assert.InRange(actor, 0, 2);
            uint saved = SavedActorsAddress + (uint)(actor * SavedActorStride);
            NativeBattleEffectArena model = NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]);
            if (!previous.ContainsKey(address))
            {
                NativeBattleEffectArena entering = NativeBattleRaldo19ModelMemory.Capture(prior.NativeGraphics!.Models[actor]);
                Assert.Equal((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(entering.Read(0, 2)) & 0x1020), state.ReadWord(saved));
                EqualSnapshot(entering, 12, state.ReadView(saved + 4, 8), 0, 8);
                EqualSnapshot(entering, 28, state.ReadView(saved + 12, 8), 0, 8);
                bornActors.Add(actor);
                capturedActor = true;
                Mark("effect313-target-current-packed-capture", current, cold);
            }
            int targetAge = task.ReadWord(12);
            if (targetAge >= 32 && graphics.Models[actor].Sequence == 6)
            {
                sequenceWait = true;
                Mark("effect313-current-sequence6-wait", current, cold);
            }
            if (targetAge is >= 1 and <= 32)
            {
                Assert.Equal(0x1020, graphics.Models[actor].Flags0 & 0x1020);
                orbit |= !model.Read(28, 6).SequenceEqual(state.Read(saved + 12, 6));
            }
            if (targetAge is 1 or 24 or 25 or 31 or 32 or 33)
            {
                Mark("effect313-before-actor-" + actor + "-age-" + targetAge, current, cold);
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous.Where(item => item.Value.Callback == 0x59C4F0))
        {
            if (tasks.ContainsKey(address))
            {
                continue;
            }
            Assert.True(task.ReadWord(12) >= 31);
            int actor = unchecked((short)task.ReadWord(14));
            uint saved = SavedActorsAddress + (uint)(actor * SavedActorStride);
            NativeBattleEffectArena model = NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]);
            EqualSnapshot(model, 12, state.ReadView(saved + 4, 8), 0, 8);
            EqualSnapshot(model, 28, state.ReadView(saved + 12, 8), 0, 8);
            Assert.Equal((uint)state.ReadWord(saved), (uint)(graphics.Models[actor].Flags0 & 0x1020));
            restoredActors.Add(actor);
            restoredActor = true;
            if (unchecked((short)task.ReadWord(0x1A)) >= 0)
            {
                Contacts++;
                Mark("effect313-contact-current-header-relative", current, cold);
            }
            Mark("effect313-restored-party-actor-" + actor, current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(item => item.Value.Callback == CleanupCallback))
        {
            cleanupAddress = address;
            cleanupQueued |= task.ReadWord(12) >= 1;
            Mark("effect313-cleanup-active", current, cold);
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous.Where(item => item.Value.Callback == CleanupCallback && item.Value.ReadWord(12) == 1))
        {
            cleanupAddress = address;
            cleanupQueued = true;
            Mark("effect313-A3-first-packet-bank-completion-request", current, cold);
        }
        for (int index = 0; index < RecordCount; index++)
        {
            uint address = RecordsAddress + (uint)(index * RecordStride);
            uint marker = state.ReadDword(address);
            if (marker != 0)
            {
                Assert.Equal(1u, marker);
                recordTypes.Add(marker);
                Assert.InRange(state.ReadWord(address + 4), 0, 16);
                if (markers.Add("effect313-record-type-" + marker))
                {
                    cold("effect313-record-type-" + marker, current);
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
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint start = cursor >= arena + 0x8000 ? arena + 0x8000u : arena;
        if (arena != 0 && cursor >= start && cursor <= start + 0x8000)
        {
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (CurrentCommands.Length > 0)
            {
                banks.Add(start == arena ? 0 : 1);
                geometry |= CurrentCommands.Any(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length == 4);
                Assert.All(CurrentCommands.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
            }
        }
        if (age is 1 or 26 or 56 or 74 or 75 or 84 or 98 or 99 or 100 or 104 or 121 or 122 or 123 or 125 or 182 or 190)
        {
            Mark("effect313-before-main-age-" + age, current, cold);
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
        File.WriteAllText(Path.Combine(output, "effect313-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), Callbacks = callbacks.Order(), CapacityEntrySeedChecked = entrySeedChecked, CapacitySamples = verifiedCapacityAges.Count, CapacityReceiptTransientPeak = expectedPeak, CapacityReceiptPeakAge = expectedPeakAge, ActualMaximumPostScanLive = maximumPostScanLive, Sequence6Wait = sequenceWait, EligibleActors = eligibleActors.Order(), BornActors = bornActors.Order(), RestoredActors = restoredActors.Order(), RecordTypes = recordTypes.Order(), Camera = camera, CameraTrack = track, CameraViewChanged = view, CameraDecision = cameraDecision, DeclinedCamera = disabledCamera, CapturedActor = capturedActor, Orbit = orbit, RestoredActor = restoredActor, Geometry = geometry, MainRetired = mainRetired, Drained = drained, CleanupQueued = cleanupQueued, CleanupCompleted = cleanupCompleted, Contacts, ContactGlyphs, Observations = observations }));
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
        Assert.True(entrySeedChecked);
        if (verifyCapacity)
        {
            Assert.Equal(99, verifiedCapacityAges.Count);
            Assert.True(maximumPostScanLive <= expectedPeak);
        }
        Assert.True(sequenceWait);
        Assert.True(eligibilityCaptured);
        Assert.Equal(eligibleActors.Order(), bornActors.Order());
        Assert.Equal(eligibleActors.Order(), restoredActors.Order());
        Assert.True(capturedActor);
        Assert.True(orbit);
        Assert.True(restoredActor);
        Assert.True(mainRetired);
        Assert.True(geometry);
        Assert.True(drained);
        Assert.True(cleanupQueued);
        Assert.True(cleanupCompleted);
        Assert.Equal(new uint[] { 1 }, recordTypes.Order());
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        foreach (uint callback in new uint[] { 0x59B9F0, 0x59BB60, 0x59BCA0, 0x59BDF0, 0x59C250, 0x59C2F0, 0x59C3E0, 0x59C4F0 })
        {
            Assert.Contains(callback, callbacks);
        }
        Assert.All(completed.NativeEffectMemory!.Effect313!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static string CallbackName(uint callback) => callback switch
    {
        MainCallback => "main",
        0x59C3E0 => "C3E0",
        0x59C2F0 => "C2F0",
        0x59BDF0 => "BDF0",
        0x59C250 => "C250",
        _ => throw new InvalidDataException("The observed early child is outside the selected capacity programme.")
    };

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
