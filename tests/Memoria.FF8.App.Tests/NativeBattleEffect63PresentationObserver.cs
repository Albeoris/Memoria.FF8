using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.App.Tests;

internal sealed class NativeBattleEffect63PresentationObserver(BattleAssets assets)
{
    private readonly HashSet<string> markers = [];
    private readonly HashSet<int> banks = [];
    private readonly HashSet<int> mainAges = [];
    private readonly HashSet<uint> particleAddresses = [];
    private readonly HashSet<int> phases = [];
    private readonly HashSet<uint> programmes = [];
    private readonly List<object> observations = [];
    private readonly List<object> pointSamples = [];
    private readonly Dictionary<int, (NativeBattleStaticMeshResource Geometry, NativeBattleGfAnimationNode Node)> morphFrames = [];
    private bool morphPrepared;
    private bool morph;
    private bool camera;
    private bool cameraTrack;
    private bool cameraView;
    private bool? cameraDecision;
    private bool declinedCamera;
    private bool uploaded;
    private bool geometry;
    private bool retired;
    private bool drained;
    private bool currentPoints;
    private bool currentView;
    private int births;
    private int particleDraws;
    private int particleRetirements;
    private int firstFrames;
    private int secondFrames;
    private int lastFrames;
    private int numericSamples;
    public int Contacts { get; private set; }
    public int ContactGlyphs { get; private set; }
    public NativeBattleLogicalCommand[] CurrentCommands { get; private set; } = [];
    public NativeBattleLogicalCommand[] CurrentFadeCommands => CurrentCommands.Where(command => command.Opcode is 0x28 or 0x2A).ToArray();

    public void Observe(BattleSnapshot prior, BattleSnapshot current, Action<string, BattleSnapshot> cold)
    {
        CurrentCommands = [];
        if (current.NativeGraphics?.OriginalEffect is not { Binding.Effect: Effect, ReturnedDescriptor: not null } effect)
        {
            return;
        }
        NativeBattleGraphicsState graphics = current.NativeGraphics;
        NativeBattleEffect63State state = current.NativeEffectMemory!.Effect63!;
        NativeBattleEffect63Resources resources = effect.Binding.Effect63Resources!;
        Dictionary<uint, NativeBattleModulePoolTask> tasks = Tasks(current);
        Dictionary<uint, NativeBattleModulePoolTask> previous = Tasks(prior);
        NativeBattleModulePoolTask? main = tasks.Values.FirstOrDefault(task => task.Callback == MainCallback);
        NativeBattleModulePoolTask? before = previous.Values.FirstOrDefault(task => task.Callback == MainCallback);
        int age = main?.ReadWord(12) ?? -1;
        int oldAge = before?.ReadWord(12) ?? (age > 0 ? age - 1 : -1);
        Assert.Equal((byte)141, graphics.Models[4].ModelId4);
        Assert.Equal(2, graphics.Models[4].CurrentBodyBoneMatrices!.Matrices.Length);
        Assert.Equal(new byte?[] { 140, 141, 141 }, graphics.Models.Skip(3).Take(3).Select(model => model.ModelId4).ToArray());
        if (current.NativeCamera?.ControlResource is { } control && control.Source.Identity == resources.Camera.Identity)
        {
            Assert.Equal(8, control.StreamOffset);
            Assert.Equal(52, control.ModelTableOffset);
            Assert.Equal(JsonSerializer.Serialize(control), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation));
            camera = true;
            bool track = current.NativeCamera.Order.Any(slot => current.NativeCamera.Tasks[slot]!.Bank.Data.AsSpan().SequenceEqual(resources.Camera.Data.AsSpan()[52..]));
            cameraTrack |= track;
            cameraView |= track && JsonSerializer.Serialize(prior.NativeCamera!.CapturePackedView1D97778()) != JsonSerializer.Serialize(current.NativeCamera.CapturePackedView1D97778());
            if (graphics.ActionGraphics?.UseActionCamera is bool decision)
            {
                cameraDecision = decision;
                declinedCamera |= !decision && current.NativeCamera.Variables[0] == -1 && current.NativeCamera.Scratch[0] == -1 && current.NativeCamera.ControlCursor is null && current.NativeCamera.Order.IsEmpty;
            }
            Mark("effect63-current-camera8-52", current, cold);
        }
        if (!uploaded && effect.ReturnedDescriptor > 0)
        {
            VerifyTim(resources.MainTim.Data.AsSpan(), current.NativeEffectMemory.Textures!.ReadVram());
            uploaded = true;
            Mark("effect63-original-tim-upload", current, cold);
        }
        if (main is not null)
        {
            uint work = state.ReadDword(WorkGlobal);
            Assert.Equal(4u, (state.ReadDword(CasterGlobal) - 0x1D972C0) / 156);
            Assert.Equal(0x1D972C0u, state.ReadDword(TargetGlobal));
            foreach ((int offset, uint source, int frame) in new (int, uint, int)[] { (0x18, FirstProgrammeAddress, Math.Clamp(age, 0, 48)), (0x100, FirstProgrammeAddress, Math.Clamp(age, 0, 48)), (0x1E8, SecondProgrammeAddress, Math.Clamp(age, 0, 45)), (0x888, ThirdProgrammeAddress, Math.Clamp(age - 36, 0, 28)) })
            {
                Assert.Equal(source, main.ReadDword(offset));
                Assert.Equal((uint)frame, main.ReadDword(offset + 4));
                programmes.Add(source);
            }
            firstFrames = Math.Max(firstFrames, (int)main.ReadDword(0x1C));
            secondFrames = Math.Max(secondFrames, (int)main.ReadDword(0x1EC));
            lastFrames = Math.Max(lastFrames, (int)main.ReadDword(0x88C));
            if (oldAge >= 0)
            {
                mainAges.Add(oldAge);
                VerifyCurrentPoints(prior, resources, oldAge);
            }
            if (oldAge is >= 36 and <= 63)
            {
                PrepareMorphFrames(resources);
                if (morphFrames.TryGetValue(oldAge - 36, out var expected))
                {
                    ImmutableArray<NativeBattleVector> vertices = NativeBattleGfMorphGeometry.MorphVertices701390(expected.Geometry.Bytes, 0, expected.Node.Word40, expected.Node.Word42, expected.Node.Scalar38);
                    Assert.Equal(189, vertices.Length);
                    int offset = checked((int)(work - 0x20DFAB8) + MorphOffset);
                    NativeBattleEffectArena actual = current.NativeEffectMemory.Arena!;
                    NativeBattleEffectArena entering = prior.NativeEffectMemory!.Arena!;
                    for (int index = 0; index < vertices.Length; index++)
                    {
                        Assert.Equal(unchecked((short)vertices[index].X), BinaryPrimitives.ReadInt16LittleEndian(actual.Read(offset + index * 8, 2)));
                        Assert.Equal(unchecked((short)vertices[index].Y), BinaryPrimitives.ReadInt16LittleEndian(actual.Read(offset + index * 8 + 2, 2)));
                        Assert.Equal(unchecked((short)vertices[index].Z), BinaryPrimitives.ReadInt16LittleEndian(actual.Read(offset + index * 8 + 4, 2)));
                        EqualBytes(entering, offset + index * 8 + 6, actual, offset + index * 8 + 6, 2);
                    }
                    morph = true;
                    Mark("effect63-actual-1512-morph-XYZ-and-retained-W", current, cold);
                }
            }
        }
        if (before is not null && main is null)
        {
            Assert.Equal(64, oldAge);
            mainAges.Add(oldAge);
            retired = true;
            Mark("effect63-main64-retired", current, cold);
        }
        if (oldAge == 54)
        {
            Contacts++;
            Mark("effect63-current-header-relative-contact54", current, cold);
        }
        int bornThisFrame = 0;
        foreach ((uint address, NativeBattleModulePoolTask task) in tasks.Where(pair => pair.Value.Callback == SpriteCallback))
        {
            int phase = task.ReadByte(0x1B);
            Assert.InRange(phase, 1, 11);
            phases.Add(phase - 1);
            particleDraws++;
            if (!previous.TryGetValue(address, out NativeBattleModulePoolTask? entering))
            {
                Assert.InRange(oldAge, 36, 38);
                Assert.Equal(1, phase);
                Assert.True(particleAddresses.Add(address));
                births++;
                bornThisFrame++;
            }
            else
            {
                Assert.Equal(entering.ReadByte(0x1B) + 1, phase);
                for (int axis = 0; axis < 3; axis++)
                {
                    Assert.Equal(unchecked((ushort)(entering.ReadWord(12 + axis * 2) + entering.ReadWord(18 + axis * 2))), task.ReadWord(12 + axis * 2));
                    Assert.Equal(unchecked((ushort)(entering.ReadWord(18 + axis * 2) + unchecked((sbyte)entering.ReadByte(24 + axis)))), task.ReadWord(18 + axis * 2));
                }
            }
        }
        foreach ((uint address, NativeBattleModulePoolTask task) in previous.Where(pair => pair.Value.Callback == SpriteCallback && !tasks.ContainsKey(pair.Key)))
        {
            Assert.Equal(11, task.ReadByte(0x1B));
            phases.Add(11);
            particleDraws++;
            particleRetirements++;
        }
        if (oldAge is >= 36 and <= 38)
        {
            Assert.Equal(10, bornThisFrame);
            Mark("effect63-ten-optional-births-age" + oldAge, current, cold);
        }
        for (int index = 0; index < graphics.Effects.Slots.Length; index++)
        {
            NativeBattleEffectTask task = graphics.Effects.Slots[index];
            if ((task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && (prior.NativeGraphics!.Effects.Slots[index].Flags0 & 1) == 0)
            {
                ContactGlyphs++;
            }
        }
        bool initialized = tasks.Values.Any(task => task.Callback == RootCallback && task.ReadByte(14) != 0);
        uint arena = state.ReadDword(WorkGlobal);
        uint cursor = state.ReadDword(CursorGlobal);
        if (initialized && cursor >= arena + FirstPacketOffset && cursor <= arena + MorphOffset)
        {
            uint start = cursor >= arena + SecondPacketOffset ? arena + SecondPacketOffset : arena + FirstPacketOffset;
            CurrentCommands = graphics.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => command.Packet.Bank == "20DFAB8" && command.Packet.Buffer == 0 && command.Packet.ByteOffset >= start - 0x20DFAB8 && command.Packet.ByteOffset < cursor - 0x20DFAB8).DistinctBy(command => (command.Packet, command.WordOffset)).ToArray();
            if (CurrentCommands.Length > 0)
            {
                banks.Add(start == arena + FirstPacketOffset ? 0 : 1);
                NativeBattleLogicalCommand[] meshes = CurrentCommands.Where(command => command.Opcode is >= 0x20 and <= 0x3F && command.Opcode is not (0x28 or 0x2A)).ToArray();
                geometry |= meshes.Length > 0;
                foreach (NativeBattleLogicalCommand command in meshes)
                {
                    Assert.Equal(4, command.VertexPayload4.Length);
                    Assert.All(command.VertexPayload4, depth => Assert.InRange(depth, 0, 65535));
                    Assert.NotNull(command.OrderingBucket);
                }
            }
        }
        if (age is 1 or 16 or 20 or 36 or 37 or 38 or 39 or 44 or 45 or 48 or 52 or 54 or 55 or 63 or 64)
        {
            Mark("effect63-before-main-age-" + age, current, cold);
        }
        drained |= effect.ReturnedDescriptor == 0 && effect.Pools.IsEmpty;
        observations.Add(new { current.Frame, graphics.Callback, MainAge = age, ProcessedAge = oldAge, Births = bornThisFrame, LiveParticles = tasks.Values.Count(task => task.Callback == SpriteCallback), MainCrtDrawsFromSuccessfulBirths = bornThisFrame * 3, EnteringGlobalCrt = prior.NativeCrtRandom!.Seed, CurrentGlobalCrt = current.NativeCrtRandom!.Seed, Cursor = cursor, Commands = CurrentCommands.Length });
    }

    public void Publish(string output) => File.WriteAllText(Path.Combine(output, "effect63-native-observations.json"), JsonSerializer.Serialize(new { Markers = markers.Order(), PacketBanks = banks.Order(), MainAges = mainAges.Order(), Programmes = programmes.Order(), ScheduledFrames = new[] { firstFrames, firstFrames, secondFrames, lastFrames }, ScheduledNodeOutputs = firstFrames * 8 + secondFrames * 18 + lastFrames * 17, UniqueNodeOutputs = firstFrames * 4 + secondFrames * 18 + lastFrames * 17, ParticleBirths = births, ParticleDraws = particleDraws, ParticleRetirements = particleRetirements, Phases = phases.Order(), MainCrtDraws = births * 3, GlobalCrtTotalNotAssumed = true, CurrentPoints = currentPoints, CurrentView = currentView, NumericSamples = numericSamples, CurrentPointSamples = pointSamples, Morph1512 = morph, Camera = camera, CameraTrack = cameraTrack, CameraView = cameraView, CameraDecision = cameraDecision, DeclinedCamera = declinedCamera, Uploaded = uploaded, Geometry = geometry, MainRetired = retired, Drained = drained, Contacts, ContactGlyphs, Observations = observations }));

    public void Accept(BattleSnapshot completed, string output)
    {
        Publish(output);
        Assert.True(camera);
        Assert.NotNull(cameraDecision);
        if (cameraDecision == true)
        {
            Assert.True(cameraTrack);
            Assert.True(cameraView);
        }
        else
        {
            Assert.True(declinedCamera);
        }
        Assert.True(uploaded);
        Assert.True(currentPoints);
        Assert.True(currentView);
        Assert.Equal(28, numericSamples);
        Assert.True(morph);
        Assert.True(geometry);
        Assert.True(retired);
        Assert.True(drained);
        Assert.Equal(Enumerable.Range(0, 65), mainAges.Order());
        Assert.Equal(new[] { 48, 45, 28 }, new[] { firstFrames, secondFrames, lastFrames });
        Assert.Equal(30, births);
        Assert.Equal(360, particleDraws);
        Assert.Equal(30, particleRetirements);
        Assert.Equal(Enumerable.Range(0, 12), phases.Order());
        Assert.Equal(2, banks.Count);
        Assert.Equal(1, Contacts);
        Assert.Equal(1, ContactGlyphs);
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(completed.NativeGraphics!.OriginalEffect!.Binding.Effect63Resources!.ReadStatic(SoundAddress, 4));
        Assert.Contains(completed.Events, item => item.NativeModuleSound?.Sound == sound);
        Assert.All(completed.NativeEffectMemory!.Effect63!.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    private void VerifyCurrentPoints(BattleSnapshot prior, NativeBattleEffect63Resources resources, int oldAge)
    {
        NativeBattleGraphicsModel model = prior.NativeGraphics!.Models[4];
        BattleCombatantDefinition caster = BattleSession.Restore(prior).GetCombatantDefinition(prior.NativeGraphics.ActorIds[4]!);
        NativeBattlePoseSource pose = assets.ReadNativeGraphicsResource(caster).PoseSource!;
        Assert.Equal(2, pose.BoneCount);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(model, pose, NativeBattleEffectArena.Clear().MarkUnknown(0, 88));
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(prepared.Model, pose);
        NativeBattleGeometryRegisterStep registers = new(prior.NativeEffectMemory!.GeometryRegisters!, prior.NativeEffectMemory.ScriptedColorRegisters!);
        short[] first = Query(FirstPointAddress);
        short[] second = Query(SecondPointAddress);
        currentPoints = true;
        _ = prior.NativeCamera!.CapturePackedView1D97778();
        currentView = true;
        if (oldAge is >= 36 and <= 63)
        {
            short x = unchecked((short)((first[0] + second[0]) / 2));
            short z = unchecked((short)((first[2] + second[2]) / 2));
            NativeBattleModelTransform target = prior.NativeGraphics.Models[0].Transform!;
            (_, int length) = NativeBattleFunguarLaser277.Basis50CBA0(x - target.Position28.X, 0, target.Position28.Z - z);
            Assert.NotEqual(0, length);
            numericSamples++;
            pointSamples.Add(new { MainAge = oldAge, PhysicalCaster = 4, model.ActiveResourceRow100, Skeleton = model.CurrentBodyBoneMatrices!.SkeletonSha256, CurrentWorld = model.GeometryWorldMatrix, CurrentBone1 = bones[1], First = first, Second = second, MidpointX = x, MidpointZ = z, Target = target.Position28, NativeLength = length, PriorView = prior.NativeCamera.CapturePackedView1D97778(), prior.NativeCamera.Projection });
        }

        short[] Query(uint address)
        {
            byte[] packed = resources.ReadStatic(address, 8);
            short selector = BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(6));
            Assert.Equal((short)1, selector);
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(prepared.Model.GeometryWorldMatrix!, bones[selector]);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(packed), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(packed.AsSpan(4))), selector);
            registers = NativeBattleGeometry.Transform(registers);
            Assert.Equal(packed, registers.Colors.NormalInput!.Value);
            return Enumerable.Range(0, 3).Select(axis => unchecked((short)registers.Colors.Ir[axis]!.Value)).ToArray();
        }
    }

    private void PrepareMorphFrames(NativeBattleEffect63Resources resources)
    {
        if (morphPrepared)
        {
            return;
        }
        NativeBattleGfAnimationSource source = resources.Act(ThirdProgrammeAddress);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[resources.WorkspaceLength(source.Address)].ToImmutableArray()).Initialize7016B0(source, resources.WorkspaceLength(source.Address));
        for (int frame = 0; frame < 28; frame++)
        {
            int selected = frame;
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if (NativeBattleActMeshRendering.Visible(node) && node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                {
                    uint address = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4)));
                    morphFrames[selected] = (resources.ReadGeometry(address), node);
                }
                return current;
            });
            workspace = step.Workspace;
        }
        morphPrepared = true;
    }

    private static void EqualBytes(NativeBattleEffectArena left, int leftOffset, NativeBattleEffectArena right, int rightOffset, int length)
    {
        var first = left.ReadSnapshot(leftOffset, length);
        var second = right.ReadSnapshot(rightOffset, length);
        Assert.Equal(first.Known, second.Known);
        for (int index = 0; index < length; index++)
        {
            if (first.Known[index])
            {
                Assert.Equal(first.Data[index], second.Data[index]);
            }
        }
    }

    private static void VerifyTim(ReadOnlySpan<byte> data, byte[] vram)
    {
        int offset = 8;
        for (int block = 0; block < 2; block++)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
            int x = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 4)..]);
            int y = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 6)..]);
            int width = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 8)..]);
            int height = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 10)..]);
            for (int row = 0; row < height; row++)
            {
                Assert.Equal(data.Slice(offset + 12 + row * width * 2, width * 2).ToArray(), vram.AsSpan(((y + row) * 1024 + x) * 2, width * 2).ToArray());
            }
            offset += length;
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
            HashSet<int> visited = [];
            int? index = pool.Head;
            while (index is int slot)
            {
                Assert.True(visited.Add(slot));
                NativeBattleModulePoolTask task = pool.Slots[slot];
                result.Add(pool.BankAddress + (uint)(slot * pool.Stride), task);
                index = task.Next;
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
