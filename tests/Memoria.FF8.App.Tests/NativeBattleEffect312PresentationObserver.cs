using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect312PresentationObserver
{
    private static readonly uint[] ExpectedCallbacks = [MainCallback, 0x59D680, 0x59D7B0, 0x59D930, 0x59DA50, 0x59DD30, 0x59F7E0, 0x59FBD0, 0x5A0030, 0x5A0150, 0x5A0290, 0x5A03D0, 0x5A0570, 0x5A0780, 0x5A0AA0, 0x5A0BF0, 0x5A0F50, RootCallback, CleanupCallback];
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<int> packetBanks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<int> raised = [];
    private readonly HashSet<int> thrown = [];
    private readonly HashSet<int> restoredRaise = [];
    private readonly HashSet<int> restoredThrow = [];
    private readonly HashSet<int> contacts = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool cameraTrack;
    private bool cameraView;
    private bool? cameraDecision;
    private bool timQueued;
    private bool paletteUploaded;
    private bool stageCapture;
    private bool environmentPackets;
    private bool geometry44;
    private bool geometry55;
    private bool randomAdvanced;
    private bool temporaryRetained;
    private bool mainRetired;
    private bool drained;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect312State state = current.NativeEffectMemory!.Effect312!;
        NativeBattleEffect312Resources resources = effect.Binding.Effect312Resources!;
        NativeBattleModulePoolTask[] tasks = Tasks(current);
        NativeBattleModulePoolTask[] previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        callbacks.UnionWith(effect.Pools.SelectMany(pool => pool.Slots).Select(task => task.Callback).Where(callback => callback != 0));
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (state.FixedBanks[shape.Bank] is { } retained)
            {
                for (int index = 0; index < shape.Capacity; index++)
                {
                    uint callback = BinaryPrimitives.ReadUInt32LittleEndian(retained.Read(index * shape.Stride + 8, 4));
                    if (callback != 0)
                    {
                        callbacks.Add(callback);
                    }
                }
            }
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Assert.Single(Pools, candidate => candidate.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal(shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        for (int actor = 3; actor <= 5; actor++)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            Assert.Equal(actor == 3 ? 44 : 6, model.CurrentBodyBoneMatrices!.Matrices.Length);
            BattleCombatantSnapshot combatant = Assert.Single(current.Combatants, item => item.Id == graphics.ActorIds[actor]);
            Assert.True(combatant.NativeParticipant!.Active);
            Assert.False(combatant.NativeParticipant.Hidden);
            Assert.False(combatant.NativeParticipant.Suspended);
            Assert.False(combatant.NativeParticipant.Untargetable);
        }
        if (graphics.ActionGraphics?.UseActionCamera is bool decision && effect.ReturnedDescriptor > 0)
        {
            if (cameraDecision is bool previousDecision)
            {
                Assert.Equal(previousDecision, decision);
            }
            cameraDecision = decision;
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(24, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[24..]));
            cameraTrack |= playing;
            cameraView |= playing && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
        }
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        timQueued |= textures.Slots.Any(slot => slot?.Source is { } source && source.Data.AsSpan().SequenceEqual(resources.MainTim.Data.AsSpan()));
        paletteUploaded |= PaletteMatches(resources.MainTim.Data.AsSpan(), textures.ReadVram());
        NativeBattleLogicalCommand[] allCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).ToArray();
        if (state.StaticPacketBound)
        {
            NativeBattleEffectArena packet = state.ReadCapturePacket();
            Assert.Equal(5, packet.Read(3, 1)[0]);
            Assert.Equal(0x28000000u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Read(4, 4)));
            if (oldAge == 43)
            {
                NativeBattleLogicalOrdering pending = graphics.PrimitiveFrame!.Pending;
                Assert.Contains(pending.Proxies, proxy => proxy.Packet == new NativeBattlePrimitiveReference("CDF6B0", 0, 0) && proxy.Flags20 == 1);
                Assert.Equal(resources.CapturePacket.AsSpan()[4..].ToArray(), packet.Read(4, CapturePacketLength - 4));
                byte[] beforeVram = prior.NativeEffectMemory!.Textures!.ReadVram();
                byte[] afterVram = textures.ReadVram();
                int changed = 0;
                int nonzero = 0;
                for (int y = 256; y < 512; y++)
                {
                    for (int x = 384; x < 640; x++)
                    {
                        int at = (y * 1024 + x) * 2;
                        ushort oldPixel = BinaryPrimitives.ReadUInt16LittleEndian(beforeVram.AsSpan(at));
                        ushort newPixel = BinaryPrimitives.ReadUInt16LittleEndian(afterVram.AsSpan(at));
                        changed += oldPixel != newPixel ? 1 : 0;
                        nonzero += newPixel != 0 ? 1 : 0;
                    }
                }
                Assert.True(changed > 100, "The current two-group stage capture did not change its VRAM rectangle.");
                Assert.True(nonzero > 100, "The stage capture contains no current geometry after the black clear.");
                Assert.Equal(1u, current.NativeEffectMemory.PrimitiveProcess!.Software!.Environment.Derived!.Mode);
                stageCapture = true;
            }
            environmentPackets |= allCommands.Any(command => command.Packet.Bank == "223CCA0");
            Mark("effect312-stage-capture-current-owners", current, cold);
        }
        NativeBattleLogicalCommand[] commands = CurrentCommands(current);
        if (commands.Length > 0)
        {
            uint common = state.ReadDword(ArenaGlobal);
            uint cursor = state.ReadDword(CursorGlobal);
            Assert.InRange(cursor, common, common + 0x18000);
            packetBanks.Add(cursor >= common + 0xC000 ? 1 : 0);
            geometry44 |= oldAge is >= 44 and <= 50 && commands.Any(command => command.Opcode is >= 0x20 and <= 0x3F);
            geometry55 |= oldAge >= 55 && commands.Any(command => command.Opcode is >= 0x20 and <= 0x3F);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Where(task => task.Callback is 0x5A0AA0 or 0x5A0BF0))
        {
            int actor = unchecked((short)task.ReadWord(14));
            Assert.InRange(actor, 0, 2);
            Assert.InRange(unchecked((short)task.ReadWord(26)), 0, 2);
            if (task.Callback == 0x5A0AA0)
            {
                raised.Add(actor);
            }
            else
            {
                thrown.Add(actor);
            }
        }
        foreach (NativeBattleModulePoolTask task in previous.Where(task => task.Callback is 0x5A0AA0 or 0x5A0BF0))
        {
            int actor = unchecked((short)task.ReadWord(14));
            if (tasks.Any(currentTask => currentTask.Callback == task.Callback && currentTask.ReadWord(14) == actor))
            {
                continue;
            }
            VerifyRestored(state, graphics.Models[actor], actor);
            if (task.Callback == 0x5A0AA0)
            {
                restoredRaise.Add(actor);
                Mark("effect312-raised-actor-restored-" + actor, current, cold);
            }
            else
            {
                restoredThrow.Add(actor);
                Mark("effect312-thrown-actor-restored-contact-" + actor, current, cold);
            }
        }
        foreach (NativeBattleEffectTask task in graphics.Effects.Slots.Where(task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex is >= 0 and <= 2))
        {
            if (task.Glyphs.AsSpan().SequenceEqual(new byte[] { 63, 0 }))
            {
                contacts.Add(task.ModelIndex);
            }
        }
        foreach (BattleEvent item in current.Events.Skip(prior.Events.Length).Where(item => item.NativeModuleSound is not null))
        {
            sounds.Add(item.NativeModuleSound!.Sound);
        }
        randomAdvanced |= prior.NativeCrtRandom != current.NativeCrtRandom;
        if (current.NativeFileStaging is { } staging)
        {
            uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
            Assert.Equal(NativeBattleTemporaryMemory.Address, cursor);
            temporaryRetained = true;
        }
        if (before is not null && main is null)
        {
            Assert.Equal(98, oldAge);
            mainRetired = true;
        }
        if (age is 1 or 8 or 44 or 54 or 55 or 76 or 99)
        {
            Mark("effect312-before-main-age-" + age, current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
            Mark("effect312-all-pools-returned", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Commands = commands.Length, Cursor = state.ReadDword(CursorGlobal), Random = current.NativeCrtRandom, Pools = effect.Pools.Select(pool => new { pool.DescriptorAddress, pool.BankAddress, Live = pool.Slots.Count(task => (task.Flags & 1) != 0) }), Actors = graphics.Models.Take(3).Select(model => new { model.Index, model.Flags0, model.Rotation12, model.Rotation14, model.Rotation16, model.RotationPadding18, model.Transform }) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(cameraTrack);
            Assert.True(cameraView);
        }
        Assert.True(timQueued);
        Assert.True(paletteUploaded);
        Assert.True(stageCapture);
        Assert.True(environmentPackets);
        Assert.True(geometry44);
        Assert.True(geometry55);
        Assert.True(randomAdvanced);
        Assert.True(temporaryRetained);
        Assert.True(mainRetired);
        Assert.True(drained);
        Assert.Equal(ExpectedCallbacks.Order(), callbacks.Order());
        Assert.Equal(new[] { 0, 1 }, packetBanks.Order());
        foreach (HashSet<int> actors in new[] { raised, thrown, restoredRaise, restoredThrow, contacts })
        {
            Assert.Equal(new[] { 0, 1, 2 }, actors.Order());
        }
        Assert.Contains(0x64209u, sounds);
        Assert.Contains(0x6420Cu, sounds);
        Assert.All(completed.NativeEffectMemory!.Effect312!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.True(NativeBattleEffect312PresentationTests.Released(completed));
        File.WriteAllText(Path.Combine(output, "effect312-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), PacketBanks = packetBanks.Order(), Sounds = sounds.Order(), Raised = raised.Order(), Thrown = thrown.Order(), RestoredRaise = restoredRaise.Order(), RestoredThrow = restoredThrow.Order(), Contacts = contacts.Order(), Camera = camera, CameraDecision = cameraDecision, CameraTrack = cameraTrack, CameraView = cameraView, TimQueued = timQueued, PaletteUploaded = paletteUploaded, StageCapture = stageCapture, EnvironmentPackets = environmentPackets, Geometry44 = geometry44, Geometry55 = geometry55, RandomAdvanced = randomAdvanced, TemporaryRetained = temporaryRetained, MainRetired = mainRetired, Drained = drained, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot current)
    {
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } || current.NativeEffectMemory?.Effect312 is not { } state)
        {
            return [];
        }
        uint common = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (cursor < common || cursor > common + 0x18000)
        {
            return [];
        }
        uint start = cursor >= common + 0xC000 ? 0xC000u : 0u;
        return current.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < cursor - common).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
    }

    private static NativeBattleModulePoolTask[] Tasks(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect } effect ? effect.Pools.SelectMany(pool => pool.Slots.Where(task => (task.Flags & 1) != 0)).ToArray() : [];

    private static void VerifyRestored(NativeBattleEffect312State state, NativeBattleGraphicsModel model, int actor)
    {
        uint saved = SavedActorsAddress + (uint)(actor * SavedActorStride);
        Assert.Equal(state.ReadWord(saved), model.Flags0 & 0x1000);
        Assert.Equal(unchecked((short)state.ReadWord(saved + 4)), model.Rotation12);
        Assert.Equal(unchecked((short)state.ReadWord(saved + 6)), model.Rotation14);
        Assert.Equal(unchecked((short)state.ReadWord(saved + 8)), model.Rotation16);
        Assert.Equal(state.ReadWord(saved + 10), model.RotationPadding18);
        NativeBattleVector position = new(unchecked((short)state.ReadWord(saved + 12)), unchecked((short)state.ReadWord(saved + 14)), unchecked((short)state.ReadWord(saved + 16)));
        Assert.Equal(position, model.Transform!.Position28);
        Assert.Equal(unchecked((short)state.ReadWord(saved + 18)), model.Transform.Vertical34);
    }

    private static bool PaletteMatches(ReadOnlySpan<byte> tim, byte[] vram)
    {
        int x = BinaryPrimitives.ReadUInt16LittleEndian(tim[12..]);
        int y = BinaryPrimitives.ReadUInt16LittleEndian(tim[14..]);
        int width = BinaryPrimitives.ReadUInt16LittleEndian(tim[16..]);
        int height = BinaryPrimitives.ReadUInt16LittleEndian(tim[18..]);
        for (int row = 0; row < height; row++)
        {
            if (!tim.Slice(20 + row * width * 2, width * 2).SequenceEqual(vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2)))
            {
                return false;
            }
        }
        return true;
    }

    private void Mark(string name, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, current);
        }
    }
}
