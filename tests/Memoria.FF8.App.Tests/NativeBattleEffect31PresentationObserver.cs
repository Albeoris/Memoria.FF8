using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect31PresentationObserver(BattleSnapshot source)
{
    private static readonly uint[] ExpectedCallbacks = [RootCallback, 0x86CD70, 0x86CE90, 0x86CF60, 0x86D100, 0x86D1D0, 0x871F60, 0x872170, 0x8723E0, 0x872700, 0x872920, 0x872B10, 0x873010, 0x8730C0, 0x8731D0, 0x8734A0];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> programmes = [];
    private readonly HashSet<uint> sounds = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<string> markers = [];
    private readonly List<object> observations = [];
    private readonly int priorStackObservations = source.NativeEffectMemory?.Effect31?.ReadCallbackStack().Observations.Length ?? 0;
    private bool initializationRequired;
    private bool initialized;
    private bool camera;
    private bool cameraTrack;
    private bool cameraView;
    private bool? cameraDecision;
    private bool timQueued;
    private bool paletteUploaded;
    private bool stageDarkened;
    private bool stageRestored;
    private bool customBirth;
    private bool customDraw;
    private bool nestedGeometry;
    private bool randomAdvanced;
    private bool temporary;
    private bool contactGlyph;
    private bool drained;
    private int hpTransitions;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        Assert.Equal(JsonSerializer.Serialize(source.Random), JsonSerializer.Serialize(current.Random));
        if (!initialized && current.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect, ReturnedDescriptor: 0 } returned && returned.Pools.IsEmpty)
        {
            return;
        }
        ushort beforeHp = prior.NativePartyPresentation!.Rows[0]!.HitPoints;
        ushort currentHp = current.NativePartyPresentation!.Rows[0]!.HitPoints;
        if (beforeHp != currentHp)
        {
            Assert.Equal(source.NativePartyPresentation!.Rows[0]!.HitPoints, beforeHp);
            Assert.Equal(Math.Max(0, beforeHp - 7), currentHp);
            hpTransitions++;
            Mark("contact-seven-presentation", current, cold);
        }
        for (int physical = 1; physical < 3; physical++)
        {
            Assert.Equal(source.NativePartyPresentation!.Rows[physical]?.HitPoints, current.NativePartyPresentation!.Rows[physical]?.HitPoints);
        }
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect31State state = current.NativeEffectMemory!.Effect31!;
        NativeBattleEffect31Resources resources = effect.Binding.Effect31Resources!;
        NativeBattleModulePoolTask[] tasks = Tasks(current);
        NativeBattleModulePoolTask[] previous = Tasks(prior);
        bool activatedNow = !initialized;
        callbacks.UnionWith(tasks.Select(task => task.Callback));
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleEffectTaskPool? beforePool = prior.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect } priorEffect ? priorEffect.Pools.FirstOrDefault(candidate => candidate.DescriptorAddress == pool.DescriptorAddress) : null;
            if (beforePool is not null)
            {
                for (int slot = 0; slot < pool.Slots.Length; slot++)
                {
                    uint callback = pool.Slots[slot].Callback;
                    if (callback != 0 && callback != beforePool.Slots[slot].Callback)
                    {
                        callbacks.Add(callback);
                    }
                }
            }
        }
        if (tasks.Any(task => task.Callback == 0x86CE90 && task.ReadByte(0x29) == 1 && task.ReadWord(0x24) == 1))
        {
            callbacks.Add(0x86D100);
            Mark("current-same-pass-empty-child", current, cold);
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = Assert.Single(Pools, candidate => candidate.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Bank, pool.BankAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal(shape.Capacity, pool.Slots.Length);
            Assert.Null(state.FixedBanks[shape.Bank]);
        }
        Assert.Equal(7, graphics.Models.Length);
        for (int physical = 0; physical < 7; physical++)
        {
            NativeBattleGraphicsModel before = source.NativeGraphics!.Models[physical];
            NativeBattleGraphicsModel model = graphics.Models[physical];
            Assert.Equal(before.ModelId4, model.ModelId4);
            Assert.Equal(before.Flags0 & 2, model.Flags0 & 2);
            Assert.Equal(before.HasLinkedModelRing140, model.HasLinkedModelRing140);
            Assert.Equal(before.LinkedModelIndex140, model.LinkedModelIndex140);
            if (physical is 0 or 3 || before.HasLinkedModelRing140 is not null)
            {
                Assert.Equal(before.ReadLinkedModelIndex140(), model.ReadLinkedModelIndex140());
            }
            Assert.Equal(before.CurrentBodyBoneMatrices?.Matrices.Length, model.CurrentBodyBoneMatrices?.Matrices.Length);
            Assert.Equal(before.CurrentLinkedBoneMatrices?.Matrices.Length, model.CurrentLinkedBoneMatrices?.Matrices.Length);
            Assert.Equal(before.Transform?.Position28, model.Transform?.Position28);
        }
        foreach (BattleCombatantSnapshot actor in current.Combatants)
        {
            BattleCombatantSnapshot initial = Assert.Single(source.Combatants, candidate => candidate.Id == actor.Id);
            Assert.Equal((initial.NativeParticipant!.ActorIndex, initial.NativeParticipant.Active, initial.NativeParticipant.Hidden, initial.NativeParticipant.Suspended, initial.NativeParticipant.Untargetable), (actor.NativeParticipant!.ActorIndex, actor.NativeParticipant.Active, actor.NativeParticipant.Hidden, actor.NativeParticipant.Suspended, actor.NativeParticipant.Untargetable));
        }
        Assert.True(state.StaticSourcesBound);
        initialized = true;
        initializationRequired |= (effect.Parameters!.Flags79 & 1) == 0;
        if (graphics.ActionGraphics?.UseActionCamera is bool decision && effect.ReturnedDescriptor > 0)
        {
            if (cameraDecision is bool earlier)
            {
                Assert.Equal(earlier, decision);
            }
            cameraDecision = decision;
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(40, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera |= activatedNow && (effect.Parameters!.Flags79 & 1) == 0;
            bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[40..]));
            cameraTrack |= playing;
            cameraView |= playing && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
        }
        NativeBattleEffectTextureState textures = current.NativeEffectMemory.Textures!;
        bool currentTim = textures.Slots.Any(slot => slot?.Source is { } item && item.Data.AsSpan().SequenceEqual(resources.MainTim.Data.AsSpan()));
        timQueued |= currentTim && (activatedNow && (effect.Parameters!.Flags79 & 1) == 0 || textures.Count > (prior.NativeEffectMemory?.Textures?.Count ?? 0));
        paletteUploaded |= timQueued && PaletteMatches(resources.MainTim.Data.AsSpan(), textures.ReadVram());
        Assert.Equal(4, current.NativeStage!.Groups.Length);
        stageDarkened |= current.NativeStage.Groups.All(group => group.ColorDepth > 0);
        stageRestored |= stageDarkened && current.NativeStage.Groups.All(group => group.ColorDepth == 0);
        NativeBattleLogicalCommand[] commands = CurrentCommands(current);
        foreach (NativeBattleLogicalCommand command in commands)
        {
            if (IsFullscreenOverlay(command))
            {
                Assert.Equal((ushort)1, command.Flags20);
            }
            else if (command.Opcode is >= 0x20 and <= 0x3F or 0xE1)
            {
                Assert.Equal((ushort)0, command.Flags20);
            }
        }
        if (commands.Length > 0)
        {
            uint origin = state.ReadDword(InitializerGlobal);
            uint cursor = state.ReadDword(CursorGlobal);
            int side = cursor >= origin + 0xD000 ? 1 : 0;
            uint start = origin + (uint)(side * 0xD000);
            Assert.InRange(cursor, start, start + 0xD000);
            banks.Add(side);
            Mark("private-packet-bank-" + side, current, cold);
        }
        foreach (NativeBattleModulePoolTask task in tasks.Where(task => task.Callback is 0x871F60 or 0x872920 or 0x872B10 or 0x8731D0))
        {
            uint programme = task.ReadDword(0x74);
            if (task.ReadDword(0x94) == programme && task.ReadDword(0x98) > 0)
            {
                programmes.Add(programme);
            }
        }
        if (programmes.Count > 0 && current.NativeFileStaging is { } staging)
        {
            NativeBattleEffectArena descriptor = NativeBattleTemporaryMemory.ReadView(staging, NativeBattleTemporaryMemory.Address, 88);
            (byte[] bytes, bool[] known) = descriptor.ReadSnapshot(0, 32);
            if (known.Take(4).All(value => value) && known.Skip(28).Take(4).All(value => value))
            {
                uint mesh = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28));
                if (MeshAddresses.Contains(mesh) && (flags is 0x2030 or 0x20F0) && commands.Any(command => command.Opcode is 0x34 or 0x36))
                {
                    callbacks.Add(0x872700);
                    nestedGeometry = true;
                    Mark("nested-animation-draw", current, cold);
                }
            }
        }
        NativeBattleModulePoolTask? custom = tasks.FirstOrDefault(task => task.Callback == 0x86D1D0);
        if (custom is not null && custom.ReadWord(0x48) > 0 && custom.ReadWord(0x4A) > 0)
        {
            customBirth = true;
            Mark("custom-node-birth", current, cold);
        }
        customDraw |= state.ReadCallbackStack().Observations.Skip(priorStackObservations).Any(observation => observation.Instruction == 0x86F4D8);
        foreach (NativeBattleModulePoolTask task in tasks.Where(task => task.Callback is RootCallback or 0x86CE90 or 0x86D1D0))
        {
            if (!previous.Any(before => before.Callback == task.Callback && before.ReadByte(0x29) == task.ReadByte(0x29)))
            {
                Mark($"phase-{task.Callback:X6}-{task.ReadByte(0x29)}", current, cold);
            }
        }
        foreach (uint programme in programmes)
        {
            bool remaining = tasks.Any(task => (task.Callback is 0x871F60 or 0x872920 or 0x872B10 or 0x8731D0) && task.ReadDword(0x74) == programme);
            if (!remaining)
            {
                Mark("programme-drained-" + programme.ToString("X"), current, cold);
            }
        }
        foreach (BattleEvent item in current.Events.Skip(prior.Events.Length).Where(item => item.NativeModuleSound is not null))
        {
            sounds.Add(item.NativeModuleSound!.Sound);
        }
        contactGlyph |= hpTransitions > 0 && graphics.Effects.Slots.Any(task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && task.Glyphs.AsSpan().SequenceEqual(new byte[] { 63, 0 }));
        randomAdvanced |= current.NativeCrtRandom != prior.NativeCrtRandom;
        if (current.NativeFileStaging is { } retainedTemporary)
        {
            Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(retainedTemporary.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            temporary = true;
        }
        if (prior.NativeActionPackets!.ActiveHeaderIndex != current.NativeActionPackets!.ActiveHeaderIndex || prior.NativeActionPackets.ActiveContactIndex != current.NativeActionPackets.ActiveContactIndex)
        {
            Mark("descriptor-header-contact-cursor", current, cold);
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
            Mark("all-native-pools-returned", current, cold);
        }
        observations.Add(new { current.Frame, graphics.Callback, Cursor = state.ReadDword(CursorGlobal), Banks = banks.Order().ToArray(), Tasks = tasks.Select(task => new { task.Callback, Phase = task.ReadByte(0x29), Age = task.ReadWord(0x24) }), Programmes = programmes.Order().ToArray(), Hp = currentHp, HpTransitions = hpTransitions, Random = current.NativeCrtRandom, StackWrites = state.ReadCallbackStack().Observations.Length, Commands = commands.Length, Stage = current.NativeStage.Groups.Select((group, index) => new { Index = index, group.ColorDepth, group.ColorFlags }) });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(initialized);
        Assert.NotNull(cameraDecision);
        if (initializationRequired)
        {
            Assert.True(camera);
            Assert.True(timQueued);
            Assert.True(paletteUploaded);
            if (cameraDecision == true)
            {
                Assert.True(cameraTrack);
                Assert.True(cameraView);
            }
        }
        Assert.True(stageDarkened);
        Assert.True(stageRestored);
        Assert.True(customBirth);
        Assert.True(customDraw);
        Assert.True(nestedGeometry);
        Assert.True(randomAdvanced);
        Assert.True(temporary);
        Assert.True(contactGlyph);
        Assert.True(drained);
        Assert.Equal(1, hpTransitions);
        Assert.Equal(Math.Max(0, source.NativePartyPresentation!.Rows[0]!.HitPoints - 7), completed.NativePartyPresentation!.Rows[0]!.HitPoints);
        Assert.Equal(ExpectedCallbacks.Order(), callbacks.Order());
        Assert.Equal(ProgrammeAddresses.Order(), programmes.Order());
        Assert.Equal(new[] { 0, 1 }, banks.Order());
        Assert.Contains(0x61B68u, sounds);
        Assert.All(completed.NativeEffectMemory!.Effect31!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(completed.NativeEffectMemory.Effect31.FixedQueues.Values, queue => Assert.NotNull(queue));
        Assert.True(NativeBattleEffect31PresentationTests.Released(completed));
        File.WriteAllText(Path.Combine(output, "effect31-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Programmes = programmes.Order(), PacketBanks = banks.Order(), Sounds = sounds.Order(), InitializationRequired = initializationRequired, Camera = camera, CameraDecision = cameraDecision, CameraTrack = cameraTrack, CameraView = cameraView, TimQueued = timQueued, PaletteUploaded = paletteUploaded, StageDarkened = stageDarkened, StageRestored = stageRestored, CustomBirth = customBirth, CustomDraw = customDraw, NestedGeometry = nestedGeometry, RandomAdvanced = randomAdvanced, Temporary = temporary, ContactGlyph = contactGlyph, HpTransitions = hpTransitions, Drained = drained, Observations = observations }));
    }

    internal static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } || snapshot.NativeEffectMemory?.Effect31 is not { } state)
        {
            return [];
        }
        uint origin = state.ReadDword(InitializerGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (cursor < origin || cursor > origin + 0x1A000 || origin < NativeBattleEffect31Controller.CommonAddress)
        {
            return [];
        }
        uint start = (cursor >= origin + 0xD000 ? origin + 0xD000 : origin) - NativeBattleEffect31Controller.CommonAddress;
        uint end = cursor - NativeBattleEffect31Controller.CommonAddress;
        return snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start && command.Packet.ByteOffset < end).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
    }

    internal static bool IsFullscreenOverlay(NativeBattleLogicalCommand command) => command.Opcode == 0x2A && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == (216u << 16) && command.Words[4] == ((216u << 16) | 320u);

    private static NativeBattleModulePoolTask[] Tasks(BattleSnapshot snapshot) => snapshot.NativeGraphics?.OriginalEffect is { Binding.Effect: Effect } effect ? effect.Pools.SelectMany(pool => pool.Slots.Where(task => (task.Flags & 1) != 0)).ToArray() : [];

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

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
