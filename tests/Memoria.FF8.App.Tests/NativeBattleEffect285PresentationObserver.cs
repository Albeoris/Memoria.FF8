using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect285PresentationObserver
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> phases = [];
    private readonly List<object> observations = [];
    private bool manualCamera;
    private bool cameraRestored;
    private bool hidden;
    private bool flagsRestored;
    private bool stageHidden;
    private bool stageVisible;
    private bool intensity;
    private bool firstAct;
    private bool secondAct;
    private bool overlap;
    private bool morph;
    private bool particles;
    private bool bonePoint;
    private int particleBirths;
    private bool flash;
    private bool geometry;
    private bool mainRetired;
    private bool uploaded;
    private bool drained;
    public bool SharedFlashObserved => flash;
    public bool WorkspaceSourcesObserved { get; private set; }
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands { get; private set; } = [];

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        CurrentFadeCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect285State state = current.NativeEffectMemory!.Effect285!;
        NativeBattleEffect285Resources resources = effect.Binding.Effect285Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? -1;
        int processed = before is not null ? oldAge : main is not null ? age - 1 : -1;
        uint arena = state.ReadDword(ArenaGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (processed >= 0)
        {
            manualCamera |= JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera!.CapturePackedView1D97778());
            Assert.Equal(35, graphics.Models[3].CurrentBodyBoneMatrices!.Matrices.Length);
            if (processed is >= 8 and <= 127)
            {
                Assert.Equal(2400u, state.ReadDword(IntensityGlobal));
                Assert.All(current.NativeStage!.Groups, group =>
                {
                    Assert.Equal((short)2400, group.ColorDepth);
                    Assert.Equal(0u, group.ColorFlags);
                });
                intensity = true;
            }
        }
        if (processed == 48)
        {
            Assert.All(graphics.Models, model => Assert.NotEqual(0, model.Flags0 & 4));
            Assert.All(current.NativeStage!.Groups, group => Assert.Equal(0, group.Flags & 2));
            hidden = true;
            stageHidden = true;
            Mark("effect285-all-seven-hide4-stage-four-clear2", current, cold);
        }
        if (processed == 86)
        {
            for (int actor = 0; actor < 7; actor++)
            {
                Assert.Equal(state.ReadDword(SavedFlagsAddress + (uint)(actor * 4)) & 4, (uint)graphics.Models[actor].Flags0 & 4);
            }
            Assert.All(current.NativeStage!.Groups, group => Assert.Equal(2, group.Flags & 2));
            flagsRestored = true;
            stageVisible = true;
            overlap = true;
            Mark("effect285-both-ACT-overlap-visibility-restored", current, cold);
        }
        NativeBattleModulePoolTask[] newborn = tasks.Values.Where(task => task.Callback == ParticleCallback && task.ReadWord(18) == 1).ToArray();
        particleBirths += newborn.Length;
        if (processed is >= 1 and <= 15 && newborn.Length == 2)
        {
            NativeBattleGraphicsModel entering = prior.NativeGraphics!.Models[3];
            foreach (uint source in new uint[] { 0x108FAB8, 0x108FAB0 })
            {
                NativeBattleEffectArena local = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(source, 8));
                int selector = BinaryPrimitives.ReadInt16LittleEndian(local.Read(6, 2));
                NativeBattleEffect285BonePointResult point = NativeBattleEffect285BonePoint.Transform(prior.NativeEffectMemory!, entering.GeometryWorldMatrix!, entering.CurrentBodyBoneMatrices!.Matrices[selector], local, NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
                Assert.Contains(newborn, task => Enumerable.Range(0, 3).All(axis => task.ReadWord(12 + axis * 2) == BinaryPrimitives.ReadUInt16LittleEndian(point.Output8.Read(axis * 2, 2))));
            }
            bonePoint = true;
            Mark("effect285-newborn-current-bone10-IR-XYZ", current, cold);
        }
        if (main is not null)
        {
            Assert.Equal(FirstProgrammeAddress, main.ReadDword(FirstWorkspaceOffset));
            Assert.Equal(SecondProgrammeAddress, main.ReadDword(SecondWorkspaceOffset));
            WorkspaceSourcesObserved = true;
        }
        foreach (NativeBattleModulePoolTask particle in tasks.Values.Where(task => task.Callback == ParticleCallback))
        {
            int phase = particle.ReadWord(18);
            Assert.InRange(phase, 1, 6);
            phases.Add(phase);
            particles = true;
        }
        foreach ((uint address, NativeBattleModulePoolTask particle) in previous.Where(item => item.Value.Callback == ParticleCallback))
        {
            if (particle.ReadWord(18) == 6 && !tasks.ContainsKey(address))
            {
                phases.Add(7);
            }
        }
        if (processed == 110)
        {
            Contacts++;
            Mark("effect285-current-header-contact110", current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        if (arena != 0)
        {
            int offset = checked((int)(arena - 0x20DFAB8));
            if (processed is >= 48 and <= 133)
            {
                var scratch = current.NativeEffectMemory.Arena!.ReadSnapshot(offset + MorphOffset, MorphLength);
                morph |= Enumerable.Range(0, MorphLength / 8).All(index => scratch.Known.AsSpan(index * 8, 6).ToArray().All(value => value));
            }
            if (cursor >= arena + FirstPacketOffset && cursor <= arena + ThirdPacketOffset)
            {
                uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
                CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                NativeBattleLogicalCommand[] shared = SharedFlashCommands(current, processed);
                CurrentCommands = CurrentCommands.Concat(shared).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
                CurrentFadeCommands = CurrentCommands.Where(IsFullViewport).ToArray();
                flash |= shared.Length > 0;
                NativeBattleLogicalCommand[] mesh = CurrentCommands.Except(CurrentFadeCommands).Where(command => command.Opcode is >= 0x20 and <= 0x3F && command.VertexPayload4.Length > 0).ToArray();
                if (mesh.Length > 0)
                {
                    banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                    geometry = true;
                    firstAct |= processed is >= 48 and <= 85;
                    secondAct |= processed is >= 87 and <= 133;
                    Assert.All(mesh.SelectMany(command => command.VertexPayload4), depth => Assert.InRange(depth, 0, 65535));
                }
            }
            if (processed == 134 && main is null)
            {
                NativeBattleCameraState camera = current.NativeCamera!;
                NativeBattleCameraPose saved = prior.NativeCamera!.CapturedPose!;
                Assert.Equal(saved.Eye, camera.CurrentPose!.Eye);
                Assert.Equal(saved.Target, camera.CurrentPose.Target);
                Assert.Equal(saved.FocalLength, camera.FocalLength);
                Assert.Equal(prior.NativeCamera.CapturedEyePadding806, camera.EyePaddingF6);
                Assert.Equal(prior.NativeCamera.CapturedTargetPadding80E, camera.TargetPaddingFE);
                Assert.Equal(prior.NativeCamera.Roll, camera.Roll);
                cameraRestored = true;
                mainRetired = true;
                Mark("effect285-main-only-retirement-selective-saved-camera", current, cold);
            }
            if (mainRetired)
            {
                byte[] header = current.NativeEffectMemory.Arena!.Read(offset + IoOffset, 5);
                if (header[4] == 1 && header[0] == 0 && header[1] == 0)
                {
                    byte[] file = resources.ReadFile(360).Data.ToArray();
                    Assert.Equal(file, current.NativeEffectMemory.Arena.Read(offset + ThirdPacketOffset, file.Length));
                    VerifyTim(file, current.NativeEffectMemory.Textures!.ReadVram());
                    uploaded = true;
                    Mark("effect285-file360-type0-current-bytes-VRAM-complete", current, cold);
                }
            }
        }
        if (age is 1 or 8 or 16 or 19 or 24 or 32 or 48 or 49 or 82 or 86 or 87 or 96 or 110 or 128 or 134)
        {
            Mark("effect285-before-main-age-" + age, current, cold);
        }
        drained |= effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty;
        observations.Add(new { current.Frame, graphics.Callback, Age = age, PreviousAge = oldAge, Processed = processed, Cursor = cursor, Commands = CurrentCommands.Length, Fades = CurrentFadeCommands.Length, Contacts, ContactGlyphs });
    }

    internal static NativeBattleLogicalCommand[] SharedFlashCommands(BattleSnapshot snapshot, int processedAge)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics ?? throw new InvalidDataException("A shared flash requires its current graphics owner.");
        if (processedAge is < 87 or > 100 || !graphics.ModuleEffects.Slots.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleModuleKind.CriticalFlash571300))
        {
            return [];
        }
        return graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => IsFullViewport(command) && command.ModuleDraw is int draw && (uint)draw < (uint)graphics.ModuleDraws.Length && graphics.ModuleDraws[draw].Kind == NativeBattleModuleDrawKind.Flash && command.Flags20 == 1 && command.OrderingBucket == 17).ToArray();
    }

    private static bool IsFullViewport(NativeBattleLogicalCommand command)
    {
        if ((command.Opcode & 0xFC) == 0x60)
        {
            return command.Words.Length == 3 && command.Words[1] == 0 && command.Words[2] == (320u | (216u << 16));
        }
        return (command.Opcode & 0xFC) == 0x28 && command.Words.Length == 5 && command.Words[1] == 0 && command.Words[2] == 320 && command.Words[3] == (216u << 16) && command.Words[4] == (320u | (216u << 16));
    }

    public void Publish(string output)
    {
        File.WriteAllText(Path.Combine(output, "effect285-native-observations.json"), JsonSerializer.Serialize(new { WorkspaceSourcesObserved, Markers = markers.Order(), PacketBanks = banks.Order(), ParticlePhases = phases.Order(), ManualCamera = manualCamera, SelectiveCameraRestore = cameraRestored, Hidden = hidden, FlagsRestored = flagsRestored, StageHidden = stageHidden, StageVisible = stageVisible, Intensity2400 = intensity, FirstAct = firstAct, SecondAct = secondAct, ActOverlap = overlap, Morph896 = morph, Particles = particles, CurrentBonePoint = bonePoint, ParticleBirths = particleBirths, SharedFlash = flash, Geometry = geometry, MainRetired = mainRetired, Uploaded = uploaded, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));
    }

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(manualCamera);
        Assert.True(cameraRestored);
        Assert.True(hidden && flagsRestored && stageHidden && stageVisible);
        Assert.True(intensity && firstAct && secondAct && overlap && morph);
        Assert.True(particles && bonePoint && geometry && mainRetired && uploaded && drained);
        Assert.Equal(72, particleBirths);
        Assert.True(flash);
        Assert.Equal(2, banks.Count);
        Assert.Equal(Enumerable.Range(1, 7), phases.Order());
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        Assert.All(completed.NativeEffectMemory!.Effect285!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private static void VerifyTim(byte[] data, byte[] vram)
    {
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 4, 2));
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 6, 2));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 8, 2));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 10, 2));
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.AsSpan(offset + 12 + row * width * 2, width * 2).ToArray(), vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2).ToArray());
            }
            offset += size;
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
