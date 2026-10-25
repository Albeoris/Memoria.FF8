using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect251PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<uint> callbacks = [];
    private readonly HashSet<uint> programmes = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<uint> sounds = [];
    private readonly List<object> observations = [];
    private bool camera;
    private bool track;
    private bool view;
    private bool? cameraDecision;
    private bool queued;
    private bool loaded;
    private bool mutable;
    private bool casterMotion;
    private bool targetMotion;
    private bool worldMotion;
    private bool boneMotion;
    private bool retired;
    private bool drained;
    private int contacts;
    private byte[]? casterSaved;
    private byte[]? targetSaved;

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect251State state = current.NativeEffectMemory!.Effect251!;
        NativeBattleEffect251Resources resources = effect.Binding.Effect251Resources!;
        NativeBattleModulePoolTask[] tasks = Tasks(current);
        NativeBattleModulePoolTask? main = tasks.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = Tasks(prior).FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        callbacks.UnionWith(tasks.Select(task => task.Callback));
        Assert.True(state.ScriptSourcesBound);
        Assert.Single(state.ScriptStorage);
        Assert.Equal(17224, state.ScriptStorage[0x1434430].Read(0, 17224).Length);
        for (int actor = 3; actor <= 4; actor++)
        {
            BattleCombatantSnapshot combatant = Assert.Single(current.Combatants, item => item.Id == graphics.ActorIds[actor]);
            Assert.Equal(actor == 4, combatant.NativeParticipant!.Suspended);
            Assert.Equal(actor == 4, combatant.NativeParticipant.Untargetable);
            Assert.True(combatant.NativeParticipant.Active);
            Assert.False(combatant.NativeParticipant.Hidden);
            Assert.Equal(actor == 3 ? 41 : 2, graphics.Models[actor].CurrentBodyBoneMatrices!.Matrices.Length);
            Assert.NotNull(graphics.Models[actor].GeometryWorldMatrix);
        }
        uint arena = state.ReadDword(ArenaGlobal);
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            NativeBattleCureFamilyPool shape = pool.DescriptorAddress == RootDescriptor ? Assert.Single(Pools) : Assert.Single(CommonPools, row => row.Descriptor == pool.DescriptorAddress);
            Assert.Equal(shape.Stride, pool.Stride);
            Assert.Equal(shape.Capacity, pool.Slots.Length);
            Assert.Equal(shape.Descriptor == RootDescriptor ? shape.Bank : arena + shape.Bank, pool.BankAddress);
        }
        if (effect.ReturnedDescriptor > 0 && graphics.ActionGraphics?.UseActionCamera is bool decision)
        {
            if (cameraDecision is bool previous)
            {
                Assert.Equal(previous, decision);
            }
            cameraDecision = decision;
        }
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            int modelOffset = BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan().Slice(4));
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(modelOffset, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool playing = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[modelOffset..]));
            track |= playing;
            view |= playing && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
        }
        if (main is not null)
        {
            casterSaved ??= main.ReadBytes(0x10, 8);
            targetSaved ??= main.ReadBytes(0x18, 8);
            Assert.Equal(casterSaved, main.ReadBytes(0x10, 8));
            Assert.Equal(targetSaved, main.ReadBytes(0x18, 8));
            foreach (int offset in new[] { 0x50, 0x218 })
            {
                uint source = main.ReadDword(offset);
                if (ProgrammeAddresses.Contains(source))
                {
                    programmes.Add(source);
                }
            }
        }
        foreach (NativeBattleModulePoolTask child in tasks.Where(task => task.Callback == 0x6F6530))
        {
            programmes.Add(child.ReadDword(0x14));
        }
        NativeBattleGraphicsState previousGraphics = prior.NativeGraphics!;
        bool casterMoved = JsonSerializer.Serialize(previousGraphics.Models[3].Transform!.Position28) != JsonSerializer.Serialize(graphics.Models[3].Transform!.Position28);
        bool targetMoved = JsonSerializer.Serialize(previousGraphics.Models[0].Transform!.Position28) != JsonSerializer.Serialize(graphics.Models[0].Transform!.Position28);
        casterMotion |= casterMoved && oldAge >= 0;
        targetMotion |= targetMoved && oldAge >= 0;
        worldMotion |= casterMoved && JsonSerializer.Serialize(previousGraphics.Models[3].GeometryWorldMatrix) != JsonSerializer.Serialize(graphics.Models[3].GeometryWorldMatrix);
        boneMotion |= casterMoved && JsonSerializer.Serialize(previousGraphics.Models[3].CurrentBodyBoneMatrices) != JsonSerializer.Serialize(graphics.Models[3].CurrentBodyBoneMatrices);
        if (before is not null && main is null)
        {
            Assert.Equal(220, oldAge);
            Assert.Equal((short)BitConverter.ToInt16(casterSaved!, 0), graphics.Models[3].Transform!.Position28.X);
            Assert.Equal((short)BitConverter.ToInt16(casterSaved!, 2), graphics.Models[3].Transform!.Position28.Y);
            Assert.Equal((short)BitConverter.ToInt16(casterSaved!, 4), graphics.Models[3].Transform!.Position28.Z);
            Assert.Equal(0, graphics.Models[0].Flags0 & 4);
            retired = true;
            Mark("effect251-main220-restored", current, cold);
        }
        NativeBattleEffectArena script = state.ScriptStorage[0x1434430];
        foreach ((int offset, int count) in new[] { (0x18, 336), (0x2C84, 80) })
        {
            int row = offset + 12;
            for (int index = 0; index < count; index++)
            {
                int at = row + index * 8;
                Assert.Equal(resources.Act(0x1434430).Data.AsSpan().Slice(at, 6).ToArray(), script.Read(at, 6));
                mutable |= !resources.Act(0x1434430).Data.AsSpan().Slice(at + 6, 2).SequenceEqual(script.Read(at + 6, 2));
            }
        }
        if (current.NativeEffectMemory.EffectIo?.BaseOffset is int ioOffset)
        {
            Assert.Equal(checked((int)(arena + IoOffset - 0x20DFAB8u)), ioOffset);
            byte[] io = current.NativeEffectMemory.Arena!.Read(ioOffset, 16);
            if (io[3] == 1)
            {
                Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(io.AsSpan(8)));
                Assert.Equal((short)713, BinaryPrimitives.ReadInt16LittleEndian(io.AsSpan(10)));
                Assert.Equal(arena + FirstPacketOffset, BinaryPrimitives.ReadUInt32LittleEndian(io.AsSpan(12)));
                queued = true;
                if (io[4] == 1 && io[0] == 0)
                {
                    NativeBattleEffectFileAsset file = resources.Files[713];
                    int retainedOffset = checked((int)(arena + FirstPacketOffset + 0x20000 - 0x20DFAB8u));
                    Assert.Equal(file.Data.AsSpan()[0x20000..].ToArray(), current.NativeEffectMemory.Arena.Read(retainedOffset, file.Data.Length - 0x20000));
                    loaded = true;
                }
            }
        }
        foreach (BattleEvent item in current.Events.Skip(prior.Events.Length).Where(item => item.NativeModuleSound is not null))
        {
            sounds.Add(item.NativeModuleSound!.Sound);
            Assert.NotNull(item.NativeModuleSound.Allocation);
        }
        if (oldAge == 219)
        {
            contacts++;
            Assert.Contains(graphics.Effects.Slots, task => task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == 0 && task.Glyphs.AsSpan().SequenceEqual(new byte[] { 63, 0 }));
            Mark("effect251-contact219-controlled-seven", current, cold);
        }
        NativeBattleLogicalCommand[] commands = CurrentCommands(current);
        if (commands.Length > 0)
        {
            NativeBattleModulePoolTask root = Assert.Single(tasks, task => task.Callback == RootCallback);
            int bank = root.ReadByte(15) == 0 ? 0 : 1;
            banks.Add(bank);
            uint start = arena + (uint)(bank == 0 ? FirstPacketOffset : SecondPacketOffset);
            Assert.InRange(state.ReadDword(CursorGlobal), start, start + 0x10000);
        }
        if (age is 1 or 35 or 54 or 55 or 73 or 83 or 114 or 138 or 163 or 176 or 177 or 192 or 199 or 208 or 219 or 220)
        {
            Mark("effect251-before-main-age-" + age, current, cold);
        }
        foreach (uint callback in new uint[] { 0x6F4A30, 0x6F4DC0, 0x6F4EC0, 0x6F5B20, 0x6F6530, 0x6F65F0 })
        {
            if (tasks.Any(task => task.Callback == callback && task.ReadWord(12) == 1))
            {
                Mark($"effect251-child-{callback:X6}-age1", current, cold);
            }
        }
        if (effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty)
        {
            drained = true;
        }
        observations.Add(new { current.Frame, graphics.Callback, MainAge = age, PreviousMainAge = oldAge, Cursor = state.ReadDword(CursorGlobal), Commands = commands.Length, Pools = effect.Pools.Select(pool => new { pool.DescriptorAddress, Live = pool.Slots.Count(task => (task.Flags & 1) != 0) }), Caster = graphics.Models[3].Transform!.Position28, Target = graphics.Models[0].Transform!.Position28, Companion = graphics.Models[4].Transform!.Position28 });
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(track);
            Assert.True(view);
        }
        Assert.True(queued);
        Assert.True(loaded);
        Assert.True(mutable);
        Assert.True(casterMotion);
        Assert.True(targetMotion);
        Assert.True(worldMotion);
        Assert.True(boneMotion);
        Assert.True(retired);
        Assert.True(drained);
        Assert.Equal(1, contacts);
        Assert.Equal(new[] { 0, 1 }, banks.Order());
        Assert.Equal(new uint[] { RootCallback, MainCallback, BurstCallback, 0x6F4A30, 0x6F4DC0, 0x6F4EC0, 0x6F5B20, 0x6F6530, 0x6F65F0 }.Order(), callbacks.Order());
        Assert.Equal(ProgrammeAddresses.Order(), programmes.Order());
        NativeBattleEffect251Resources resources = completed.NativeGraphics!.OriginalEffect!.Binding.Effect251Resources!;
        Assert.All(SoundAddresses, address => Assert.Contains(BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(address, 4)), sounds));
        Assert.All(completed.NativeEffectMemory!.Effect251!.FixedBanks.Values, bank => Assert.NotNull(bank));
        Assert.All(completed.NativeEffectMemory.Effect251.CommonQueueStorage.Values, queue => Assert.NotNull(queue));
        File.WriteAllText(Path.Combine(output, "effect251-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), Callbacks = callbacks.Order(), Programmes = programmes.Order(), PacketBanks = banks.Order(), Sounds = sounds.Order(), Camera = camera, CameraDecision = cameraDecision, CameraTrack = track, CameraViewChanged = view, Queued = queued, Loaded = loaded, MutatedW = mutable, CasterMotion = casterMotion, TargetMotion = targetMotion, WorldMotion = worldMotion, BoneMotion = boneMotion, Contacts = contacts, MainRetired = retired, Drained = drained, Observations = observations }));
    }

    public static NativeBattleLogicalCommand[] CurrentCommands(BattleSnapshot snapshot)
    {
        NativeBattleEffect251State? state = snapshot.NativeEffectMemory?.Effect251;
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } || state is null)
        {
            return [];
        }
        NativeBattleModulePoolTask? root = Tasks(snapshot).FirstOrDefault(task => task.Callback == RootCallback);
        if (root is null)
        {
            return [];
        }
        uint buffer = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        uint start = buffer + (uint)(root.ReadByte(15) == 0 ? FirstPacketOffset : SecondPacketOffset);
        return snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8u && command.Packet.ByteOffset < cursor - 0x20DFAB8u).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
    }

    private static NativeBattleModulePoolTask[] Tasks(BattleSnapshot snapshot)
    {
        List<NativeBattleModulePoolTask> tasks = [];
        if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect } effect)
        {
            return [];
        }
        foreach (NativeBattleEffectTaskPool pool in effect.Pools)
        {
            HashSet<int> visited = [];
            for (int? slot = pool.Head; slot is int index; slot = pool.Slots[index].Next)
            {
                Assert.True(visited.Add(index));
                tasks.Add(pool.Slots[index]);
            }
        }
        return tasks.ToArray();
    }

    private void Mark(string name, BattleSnapshot snapshot, Action<string, BattleSnapshot> cold)
    {
        if (markers.Add(name))
        {
            cold(name, snapshot);
        }
    }
}
