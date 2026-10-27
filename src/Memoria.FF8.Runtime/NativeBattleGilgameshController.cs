using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleFileStaging? Staging { get; init; }
}
public abstract record NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshPacketCursorAdvance(int Bytes) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshSprites(NativeBattleModulePacketBatch Batch) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshService(NativeBattleGilgameshDirectorOperation Operation) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshContacts(int First, int Count) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshCameraPublish(NativeBattleGuardianCameraStep Step) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshTextureLoad(uint CompletionAddress) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshPcRequest(int Mode) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshInputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short Dqa, int Dqb, NativeBattleSummonColors Colors, Func<byte, NativeBattleModuleAnchor> ReadAnchor571400, Func<int> Pending508500, Func<NativeBattleCameraPose> ReadCameraPose, Func<NativeBattleGilgameshFrame, NativeBattleGilgameshOperation, NativeBattleGilgameshFrame> Execute)
{
    public Func<NativeBattleCameraProjection>? ReadProjection { get; init; }
    public Func<short>? ReadFocalShadow { get; init; }
    public Func<byte, short>? ReadModelWord26 { get; init; }
    public Func<byte, NativeBattlePackedMatrix32>? ReadModelWorld32 { get; init; }
}

public static partial class NativeBattleGilgameshController
{
    public static NativeBattleGilgameshFrame Initialize58DCF0(NativeBattleGilgameshFrame frame, NativeBattleActionPacketsState packets, ImmutableArray<NativeBattleGraphicsModel> models)
    {
        NativeBattleGilgameshProcess retained = frame.Memory.Gilgamesh ?? throw new NotSupportedException("Native Gilgamesh requires its retained process model owners; explicitly migrate a needed snapshot.");
        retained.Validate();
        NativeBattleGilgameshEntryStep initialized = NativeBattleGilgameshEntry.Initialize58DCF0(frame.Memory, frame.Effect, packets, models);
        return frame with { Memory = initialized.Memory with { Gilgamesh = retained with { Entry = initialized.State, Variant = NativeBattleGilgameshProfile.ForEffect(frame.Effect.Binding.Effect).Variant } }, Effect = initialized.Effect };
    }

    public static NativeBattleGilgameshFrame Advance596B70(NativeBattleGilgameshFrame frame, NativeBattleGilgameshEffectResources resources, NativeBattleGilgameshInputs inputs)
    {
        if (resources.Files.Effect != frame.Effect.Binding.Effect || resources.Director.Effect != frame.Effect.Binding.Effect)
        {
            throw new InvalidDataException("Native Gilgamesh resources belong to another sword attack.");
        }
        Execution execution = new(frame, resources, inputs);
        NativeBattleGuardianPoolStep step = NativeBattleGilgameshScheduler.Advance(Pools(frame.Effect), (pools, descriptor, slot) =>
        {
            execution.Frame = execution.Frame with { Effect = execution.Frame.Effect with { Pools = [pools.Main, pools.Children, pools.Particles!] } };
            NativeBattleEffectTaskPool pool = execution.Frame.Effect.Pools.Single(current => current.DescriptorAddress == descriptor);
            int result = execution.Advance(checked(pool.BankAddress + (uint)(slot * pool.Stride)));
            return new(Pools(execution.Frame.Effect), result);
        });
        return execution.Frame with { Effect = execution.Frame.Effect with { Pools = [step.Pools.Main, step.Pools.Children, step.Pools.Particles!], ReturnedDescriptor = step.Survivors == 0 ? 0 : step.Pools.Main.DescriptorAddress } };
    }

    private static NativeBattleGuardianEffectPools Pools(NativeBattleOriginalEffectState effect)
    {
        if (effect.Binding.Entry != NativeBattleGilgameshProfile.ForEffect(effect.Binding.Effect).Entry || effect.Pools.IsDefault || effect.Pools.Length != 3)
        {
            throw new InvalidDataException("Native Gilgamesh requires its executed Excalipoor entry and three raw task banks.");
        }
        return new(effect.Pools[0], effect.Pools[1], effect.Pools[2]);
    }

    private sealed partial class Execution(NativeBattleGilgameshFrame frame, NativeBattleGilgameshEffectResources resources, NativeBattleGilgameshInputs inputs)
    {
        public NativeBattleGilgameshFrame Frame { get; set; } = frame;
        private NativeBattleCameraProjection Projection => inputs.ReadProjection?.Invoke() ?? inputs.Projection;
        private NativeBattleGilgameshProcess Shared => Frame.Memory.Gilgamesh ?? throw new NotSupportedException("Native Gilgamesh requires its retained process.");
        private NativeBattleActionMatrix World => Shared.Entry?.World ?? throw new NotSupportedException("Native Gilgamesh requires its executed entry world matrix.");
        private NativeBattleActionMatrix WorldView => Shared.WorldView ?? throw new NotSupportedException("Native Gilgamesh requires its current published world-view matrix.");
        private NativeBattleEffectArena Arena => Frame.Memory.Arena ?? throw new NotSupportedException("Native Gilgamesh requires its current arena.");
        private NativeBattleEffectParameters Parameters => Frame.Effect.Parameters ?? throw new NotSupportedException("Native Gilgamesh requires its current action parameters.");
        private NativeBattleActionHeaderSlot Header => inputs.Packets.Headers[Parameters.HeaderIndex7C];
        private NativeBattleEffectTaskPool Children => Frame.Effect.Pools[1];
        private bool Paused => (inputs.GraphicsFlags & 0x201) != 0;
        private NativeBattleGilgameshZantetsukenInputs ZantetsukenInputs(ImmutableArray<NativeBattleGilgameshZantetsukenContactRow> contacts) => new(inputs.GraphicsFlags, contacts, actor => inputs.ReadModelWord26?.Invoke(actor) ?? throw new NotSupportedException("Zantetsuken requires its actual model height WORD."), actor => inputs.ReadModelWorld32?.Invoke(actor) ?? throw new NotSupportedException("Zantetsuken requires its retained model world matrix."), inputs.Execute);

        private void SetShared(NativeBattleGilgameshProcess state) => Frame = Frame with { Memory = Frame.Memory with { Gilgamesh = state } };
        private void SetChildren(NativeBattleEffectTaskPool pool) => Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, pool) } };
        private void Emit(NativeBattleGilgameshOperation operation) => Frame = inputs.Execute(Frame, operation);

        private (int Pool, int Slot) Locate(uint address)
        {
            for (int index = 0; index < Frame.Effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = Frame.Effect.Pools[index];
                uint offset = unchecked(address - pool.BankAddress);
                if (offset < pool.Slots.Length * pool.Stride && offset % pool.Stride == 0)
                {
                    return (index, (int)(offset / pool.Stride));
                }
            }
            throw new InvalidDataException("Native Gilgamesh callback address leaves its physical raw banks.");
        }

        private NativeBattleModulePoolTask Read(uint address)
        {
            (int pool, int slot) = Locate(address);
            return Frame.Effect.Pools[pool].Slots[slot];
        }

        private void Write(uint address, NativeBattleModulePoolTask task)
        {
            (int index, int slot) = Locate(address);
            NativeBattleEffectTaskPool pool = Frame.Effect.Pools[index];
            Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback))) } };
        }

        private uint Append(uint callback, bool particle = false)
        {
            int index = particle ? 2 : 1;
            NativeBattleEffectTaskPool pool = Frame.Effect.Pools[index];
            int slot;
            (pool, slot) = pool.Append(callback);
            pool = pool.WriteSlot(slot, pool.Slots[slot].WriteWord(12, 0).WriteWord(14, 0));
            Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(index, pool) } };
            return checked(pool.BankAddress + (uint)(slot * pool.Stride));
        }

        public int Advance(uint address)
        {
            NativeBattleModulePoolTask task = Read(address);
            switch (task.Callback)
            {
                case 0x592300:
                    NativeBattleGilgameshDirectorStep director = NativeBattleGilgameshDirector.Advance592300(task, new(inputs.GraphicsFlags, () => SetShared(Shared with { WorldView = NativeBattleGfMatrices.Compose56C2F0(Projection.View ?? throw new NotSupportedException("Native Gilgamesh requires its current camera view."), World) }), inputs.Pending508500, Director) { Effect = Frame.Effect.Binding.Effect });
                    Write(address, director.Task);
                    return director.Result;
                case 0x63E9C0:
                    NativeBattleGuardianCameraTask camera = Shared.CameraTask ?? throw new NotSupportedException("Native Gilgamesh requires its current camera task.");
                    NativeBattleGuardianCameraStep cameraStep = NativeBattleGuardianCameraProgram.Advance63E9C0(camera, World, inputs.ReadCameraPose(), (inputs.GraphicsFlags & 1) != 0);
                    SetShared(Shared with { CameraTask = cameraStep.Task });
                    Write(address, task.WriteWord(12, cameraStep.Task.Frame).WriteDword(20, checked(NativeBattleGilgameshProfile.ForEffect(Frame.Effect.Binding.Effect).CameraAddress + (uint)cameraStep.Task.Offset)));
                    Emit(new NativeBattleGilgameshCameraPublish(cameraStep));
                    return cameraStep.Result;
                case 0x592BC0:
                    NativeBattleGilgameshFlashStep flash = NativeBattleGilgameshFlash.Step592BC0(task, inputs.GraphicsFlags);
                    Write(address, flash.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(flash.Batch));
                    return flash.Result;
                case 0x592E90:
                case 0x592F90:
                case 0x5930C0:
                    NativeBattleGilgameshOpeningStep opening = NativeBattleGilgameshOpening.Step(task, resources.Opening, WorldView, Projection, inputs.GraphicsFlags, inputs.Colors);
                    Write(address, opening.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(opening.Batch));
                    return opening.Result;
                case 0x591470:
                    NativeBattleGilgameshFlareStep flare = NativeBattleGilgameshFlare.Step591470(task, resources.Flare, Projection, inputs.GraphicsFlags);
                    Write(address, flare.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(flare.Batch));
                    return flare.Result;
                case 0x591F50:
                    NativeBattleGilgameshFinalRingStep ring = NativeBattleGilgameshFinalRing.Step591F50(task, resources.FinalRing, Projection.View ?? throw new NotSupportedException("Native ring requires its current view."), Projection, inputs.GraphicsFlags, inputs.Colors);
                    Write(address, ring.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(ring.Batch));
                    return ring.Result;
                case 0x58DED0:
                    if (Paused)
                    {
                        return 0;
                    }
                    ImmutableArray<NativeBattleGilgameshZantetsukenContactRow> zantetsukenContacts = default;
                    if (task.ReadWord(12) == 16)
                    {
                        int zantetsukenFirst = Header.FirstContactIndex ?? throw new NotSupportedException("Zantetsuken requires its captured first contact pointer.");
                        zantetsukenContacts = Enumerable.Range(zantetsukenFirst, Header.ReadByte(16)).Select(index => inputs.Packets.ReadContact(index).Primary).Select(contact => new NativeBattleGilgameshZantetsukenContactRow(contact.ActorIndex, contact.HitFlags3)).ToImmutableArray();
                    }
                    NativeBattleGilgameshZantetsukenStep zantetsuken = NativeBattleGilgameshZantetsukenAttack.Advance(Frame, address, ZantetsukenInputs(zantetsukenContacts));
                    Frame = zantetsuken.Frame;
                    return zantetsuken.Result;
                case 0x58E3E0:
                    NativeBattleGilgameshZantetsukenStep rotation327 = NativeBattleGilgameshZantetsukenLifecycle.AdvanceRotation(Frame, address, ZantetsukenInputs(default));
                    Frame = rotation327.Frame;
                    return rotation327.Result;
                case 0x58E120:
                    return ZantetsukenFlare(address);
                case 0x58E570:
                    NativeBattleGilgameshZantetsukenStep actor327 = NativeBattleGilgameshZantetsukenLifecycle.AdvanceActor(Frame, address, ZantetsukenInputs(default));
                    Frame = actor327.Frame;
                    return actor327.Result;
                case 0x58F940:
                    NativeBattleGilgameshZantetsukenStep stage327 = NativeBattleGilgameshZantetsukenLifecycle.AdvanceStage(Frame, address, ZantetsukenInputs(default));
                    Frame = stage327.Frame;
                    return stage327.Result;
                case 0x590510:
                    if (Paused)
                    {
                        return 0;
                    }
                    int masamuneFirst = Header.FirstContactIndex ?? throw new NotSupportedException("Masamune requires its captured physical contact pointer.");
                    int masamuneCount = Header.ReadByte(16);
                    ImmutableArray<byte> masamuneActors = Enumerable.Range(masamuneFirst, masamuneCount).Select(index => inputs.Packets.ReadContact(index).Primary.ActorIndex).ToImmutableArray();
                    NativeBattleGilgameshMasamuneAttackStep masamune = NativeBattleGilgameshMasamuneAttack.Advance(Frame, address, inputs.GraphicsFlags, masamuneActors, inputs.Execute);
                    Frame = masamune.Frame;
                    if (masamune.Contact)
                    {
                        Emit(new NativeBattleGilgameshContacts(masamuneFirst, masamuneCount));
                    }
                    return masamune.Result;
                case 0x590790:
                    return MasamuneFlare(address);
                case 0x5908D0:
                    return MasamuneBurst(address, false);
                case 0x590A10:
                    return MasamuneBurst(address, true);
                case 0x590B60:
                    return MasamuneParticles(address, false);
                case 0x590F10:
                    return MasamuneParticles(address, true);
                case 0x591250:
                case 0x5920E0:
                    int first = Header.FirstContactIndex ?? throw new NotSupportedException("Native Excalipoor requires its physical contact pointer.");
                    int count = Header.ReadByte(16);
                    ImmutableArray<byte> actors = Enumerable.Range(first, count).Select(index => inputs.Packets.ReadContact(index).Primary.ActorIndex).ToImmutableArray();
                    NativeBattleGilgameshAttackStep attack = NativeBattleGilgameshExcalipoorAttack.Advance(NativeBattleGilgameshAttackProfile.ForEffect(Frame.Effect.Binding.Effect), task, Children, Frame.Random, inputs.GraphicsFlags, actors, inputs.ReadAnchor571400);
                    SetChildren(attack.Children);
                    Write(address, attack.Task);
                    Frame = Frame with { Random = attack.Random };
                    if (attack.Contact)
                    {
                        Emit(new NativeBattleGilgameshContacts(first, count));
                    }
                    return attack.Result;
                case 0x5915C0:
                case 0x591850:
                case 0x591B80:
                    NativeBattleGilgameshParticleStep particles = NativeBattleGilgameshParticles.Step(task, Arena, Frame.Random, resources.Particles, Projection.View ?? throw new NotSupportedException("Native Gilgamesh particles require the camera view."), Projection, inputs.GraphicsFlags);
                    return PublishParticles(address, particles);
                case 0x5948A0:
                case 0x594CA0:
                    NativeBattleGilgameshSwordParticleStep swordParticles = NativeBattleGilgameshSwordParticles.Step(task, Arena, Frame.Random, resources.SwordParticles, WorldView, Projection, inputs.GraphicsFlags, ReadSwordBone, inputs.Colors);
                    Frame = Frame with { Memory = Frame.Memory with { Arena = swordParticles.Arena }, Random = swordParticles.Random };
                    Write(address, swordParticles.UpdatedTask);
                    foreach (NativeBattleGfGeometryBatch batch in swordParticles.MeshPackets)
                    {
                        Emit(new NativeBattleGilgameshPackets(batch));
                    }
                    foreach (NativeBattleModulePacketBatch batch in swordParticles.SpritePackets)
                    {
                        Emit(new NativeBattleGilgameshSprites(batch));
                    }
                    return swordParticles.Result;
                case 0x593770:
                case 0x5939C0:
                    NativeBattleGilgameshParticleStep common = NativeBattleGilgameshCommonParticles.Step(task, Arena, Frame.Random, resources.CommonParticles, WorldView, Projection, inputs.GraphicsFlags);
                    return PublishParticles(address, common);
                case 0x593E80:
                    NativeBattleGilgameshPointStep points = NativeBattleGilgameshPointBurst.Step593E80(task, Arena, Frame.Random, resources.Points, inputs.GraphicsFlags, point => NativeBattleModuleProjection.Project45FAA0(WorldView, point, Projection, inputs.Dqa, inputs.Dqb));
                    Frame = Frame with { Memory = Frame.Memory with { Arena = points.Arena }, Random = points.Random };
                    Write(address, points.Task);
                    Emit(new NativeBattleGilgameshPackets(points.Packets));
                    return points.Result;
                case 0x593600:
                case 0x594210:
                    NativeBattleGilgameshMeshAuxiliaryStep mesh = NativeBattleGilgameshMeshAuxiliaries.Step(task, resources.MeshAuxiliaries, WorldView, Projection, inputs.GraphicsFlags, inputs.Colors);
                    Write(address, mesh.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(mesh.Batch));
                    return mesh.Result;
                case 0x593C70:
                    NativeBattleGilgameshNestedAppend nested = NativeBattleGilgameshCommonParticles.Step593C70(task, Children, Frame.Random, inputs.GraphicsFlags);
                    SetChildren(nested.Pool);
                    Frame = Frame with { Random = nested.Random };
                    Write(address, nested.UpdatedTask);
                    return nested.Result;
                case 0x593D20:
                    NativeBattleGilgameshMeshAuxiliaryStep nestedMesh = NativeBattleGilgameshCommonParticles.Step593D20(task, resources.CommonParticles, WorldView, Projection, inputs.GraphicsFlags, inputs.Colors);
                    Write(address, nestedMesh.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(nestedMesh.Batch));
                    return nestedMesh.Result;
                case 0x5943D0:
                    NativeBattleGilgameshSwordTrailStep trail = NativeBattleGilgameshSwordTrail.Step5943D0(Shared.SwordTrail, task, Shared.Variant ?? throw new NotSupportedException("Native5943D0 requires its initialized sword selector."), ReadSwordBone, WorldView, Projection, Frame.Memory.ScriptedGraphics, inputs.Colors, resources.SwordTrail, inputs.GraphicsFlags);
                    SetShared(Shared with { SwordTrail = trail.State });
                    Write(address, trail.UpdatedTask);
                    if (trail.PoolCursorAdvance is int advance)
                    {
                        Emit(new NativeBattleGilgameshPackets(trail.Packets));
                        Emit(new NativeBattleGilgameshPacketCursorAdvance(checked(advance - trail.Packets.AllocationBytes)));
                    }
                    return trail.Result;
                case 0x594FD0:
                    NativeBattleGilgameshSwordStep swords = NativeBattleGilgameshSwords.Step594FD0(Shared.Models, Arena, task, resources.Models.StaticModels, WorldView, Projection, Frame.Memory.ScriptedGraphics, inputs.GraphicsFlags);
                    Frame = Frame with { Memory = Frame.Memory with { Arena = swords.Arena, Gilgamesh = Shared with { Models = swords.ModelMemory } } };
                    Write(address, swords.UpdatedTask);
                    foreach (NativeBattleGfGeometryBatch batch in swords.Packets)
                    {
                        Emit(new NativeBattleGilgameshPackets(batch));
                    }
                    return swords.Result;
                case 0x595EE0:
                    NativeBattleGilgameshModelAStep modelA = NativeBattleGilgameshModelA.Step595EE0(Shared.Models, Arena, task, resources.Models.StreamModel746, WorldView, Projection, Frame.Memory.ScriptedGraphics, inputs.GraphicsFlags);
                    Frame = Frame with { Memory = Frame.Memory with { Arena = modelA.Arena, Gilgamesh = Shared with { Models = modelA.ModelMemory } } };
                    Write(address, modelA.UpdatedTask);
                    Emit(new NativeBattleGilgameshPackets(modelA.Packets));
                    return modelA.Result;
                case 0x593250:
                    NativeBattleCameraPoint eye = inputs.ReadCameraPose().Eye;
                    NativeBattleGilgameshOpeningFlareStep openingFlare = NativeBattleGilgameshOpeningFlare.Step593250(task, Shared.OpeningFlare, resources.OpeningFlare, new(unchecked((short)eye.X), unchecked((short)eye.Y), unchecked((short)eye.Z)), Projection.View ?? throw new NotSupportedException("Native opening flare requires its actual camera view."), Projection, inputs.Dqa, inputs.Dqb, inputs.GraphicsFlags);
                    SetShared(Shared with { OpeningFlare = openingFlare.State });
                    Write(address, openingFlare.UpdatedTask);
                    foreach (NativeBattleModulePacketBatch batch in openingFlare.Packets)
                    {
                        Emit(new NativeBattleGilgameshSprites(batch));
                    }
                    return openingFlare.Result;
                case 0x595D50:
                case 0x5966B0:
                case 0x596710:
                case 0x596800:
                case 0x596970:
                case 0x596A60:
                    NativeBattleGilgameshModelBankStep bank = NativeBattleGilgameshModelBanks.Step(Shared.Models, Arena, task, resources.Models, WorldView, Projection, inputs.Colors, inputs.GraphicsFlags);
                    Frame = Frame with { Memory = Frame.Memory with { Arena = bank.Arena, Gilgamesh = Shared with { Models = bank.ModelMemory } } };
                    Write(address, bank.UpdatedTask);
                    foreach (NativeBattleGfGeometryBatch batch in bank.Packets)
                    {
                        Emit(new NativeBattleGilgameshPackets(batch));
                    }
                    return bank.Result;
                case 0x592A00:
                case 0x592A50:
                    NativeBattleGilgameshCommonStep fade = NativeBattleGilgameshCommonTasks.StepFade(task, inputs.GraphicsFlags, (mode, _, _, _, _) => Emit(new NativeBattleGilgameshPcRequest(mode)));
                    Write(address, fade.UpdatedTask);
                    return fade.Result;
                case 0x592AE0:
                case 0x592B60:
                    NativeBattleGilgameshCommonStep stage = NativeBattleGilgameshCommonTasks.StepStage(task, inputs.GraphicsFlags, (depth, color) => Emit(new NativeBattleGilgameshService(new(NativeBattleGilgameshDirectorService.Stage, depth, color))));
                    Write(address, stage.UpdatedTask);
                    return stage.Result;
                case 0x596B30:
                    NativeBattleGilgameshCommonStep texture = NativeBattleGilgameshCommonTasks.Step596B30(task, NativeBattleEffectNodeMemory.ArenaAddress, address, (_, completion, _) =>
                    {
                        Emit(new NativeBattleGilgameshTextureLoad(completion));
                        return Read(address);
                    });
                    Write(address, texture.UpdatedTask);
                    return texture.Result;
                default:
                    throw new NotSupportedException($"Native Gilgamesh callback{task.Callback:X8} requires its complete original contract.");
            }
        }

        private NativeBattleVector ReadSwordBone(int index, byte selector, short fraction)
        {
            if (index < 0 || index >= 4)
            {
                throw new InvalidDataException("Native sword bone selector leaves its four physical models.");
            }
            NativeBattleCactuarRawModelState state = Shared.Models.Read(checked(0x21FF4F0u + (uint)index * 0x9C)).State;
            ReadOnlySpan<byte> bytes = state.Model.AsSpan();
            NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(offset => BinaryPrimitives.ReadInt16LittleEndian(state.Model.AsSpan().Slice(0x40 + offset * 2))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x54)), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x58)), BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0x5C)), 4096);
            return NativeBattlePoseMatrices.ReadEffectOwnedBone(resources.Models.StaticModels[index].PoseSource, state.DecodedPose ?? throw new NotSupportedException("Native sword particles require the current selected sword pose."), matrix, selector, fraction);
        }

        private int PublishParticles(uint address, NativeBattleGilgameshParticleStep step)
        {
            Frame = Frame with { Memory = Frame.Memory with { Arena = step.Arena }, Random = step.Random };
            Write(address, step.UpdatedTask);
            foreach (NativeBattleModulePacketBatch batch in step.Packets)
            {
                Emit(new NativeBattleGilgameshSprites(batch));
            }
            return step.Result;
        }

        private void Director(NativeBattleGilgameshDirectorOperation operation)
        {
            switch (operation.Service)
            {
                case NativeBattleGilgameshDirectorService.Camera:
                    NativeBattleGuardianCameraProcess camera = Frame.Memory.GuardianCameraProcess ?? throw new NotSupportedException("Native Gilgamesh requires its retained camera banks.");
                    SetShared(Shared with { CameraTask = NativeBattleGuardianCameraProgram.Begin63E960(camera, resources.Director.CameraProgram.Data, 0) });
                    uint address = Append(0x63E9C0);
                    Write(address, Read(address).WriteDword(20, NativeBattleGilgameshProfile.ForEffect(Frame.Effect.Binding.Effect).CameraAddress).WriteDword(24, 0x21FF3A0).WriteDword(28, 0x2200FC0));
                    break;
                case NativeBattleGilgameshDirectorService.AppendChild:
                case NativeBattleGilgameshDirectorService.AppendParticle:
                    Append(checked((uint)operation.First), operation.Service == NativeBattleGilgameshDirectorService.AppendParticle);
                    break;
                case NativeBattleGilgameshDirectorService.Initializer:
                    Initialize(checked((uint)operation.First));
                    break;
                case NativeBattleGilgameshDirectorService.FadeTask:
                    SetChildren(NativeBattleGilgameshCommonTasks.Append5929B0(Children, operation.First, checked((ushort)operation.Second)));
                    break;
                default:
                    Emit(new NativeBattleGilgameshService(operation));
                    break;
            }
        }

        private void Initialize(uint callback)
        {
            switch (callback)
            {
                case 0x5943A0:
                    NativeBattleGilgameshSwordTrailInitialization trail = NativeBattleGilgameshSwordTrail.Initialize5943A0(Children, Shared.SwordTrail);
                    SetChildren(trail.Pool);
                    SetShared(Shared with { SwordTrail = trail.State });
                    break;
                case 0x5947F0:
                    NativeBattleGilgameshSwordParticleInitialization swords = NativeBattleGilgameshSwordParticles.Initialize5947F0(Children, Frame.Effect.Pools[2], unchecked((short)(Shared.Variant ?? throw new NotSupportedException("Native5947F0 requires its initialized variant WORD."))), resources.SwordParticles);
                    Frame = Frame with { Effect = Frame.Effect with { Pools = Frame.Effect.Pools.SetItem(1, swords.Children).SetItem(2, swords.Particles) } };
                    break;
                case 0x592D00:
                    NativeBattleGilgameshOpeningInitialization opening = NativeBattleGilgameshOpening.Initialize592D00(Children, Frame.Random);
                    SetChildren(opening.Pool);
                    Frame = Frame with { Random = opening.Random };
                    break;
                case 0x5931E0:
                    SetChildren(NativeBattleGilgameshOpeningFlare.Initialize5931E0(Children, World));
                    break;
                case 0x593550:
                    NativeBattleGilgameshMeshAuxiliaryInitialization first = NativeBattleGilgameshMeshAuxiliaries.Initialize593550(Children, Frame.Random);
                    SetChildren(first.Pool);
                    Frame = Frame with { Random = first.Random };
                    break;
                case 0x594160:
                    NativeBattleGilgameshMeshAuxiliaryInitialization second = NativeBattleGilgameshMeshAuxiliaries.Initialize594160(Children, Frame.Random, resources.MeshAuxiliaries);
                    SetChildren(second.Pool);
                    Frame = Frame with { Random = second.Random };
                    break;
                case 0x592AB0:
                    SetChildren(NativeBattleGilgameshCommonTasks.Append592AB0(Children));
                    break;
                case 0x592B30:
                    SetChildren(NativeBattleGilgameshCommonTasks.Append592B30(Children));
                    break;
                case 0x5904D0:
                case 0x5904F0:
                    Emit(new NativeBattleGilgameshService(new(NativeBattleGilgameshDirectorService.Initializer, checked((int)callback))));
                    break;
                default:
                    throw new NotSupportedException($"Native Gilgamesh initializer{callback:X8} requires its complete original contract.");
            }
        }
    }
}
