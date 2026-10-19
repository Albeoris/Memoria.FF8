using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeDuel215(NativeBattleGraphicsState graphics, NativeBattleDuel215Resources resources)
    {
        NativeBattleDuel215Frame frame = NativeBattleDuel215Controller.Enter69AB90(NativeEffectMemory ?? throw new NotSupportedException("Native Duel215 requires retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel215 requires its entered module."), NativeFileStaging ?? throw new NotSupportedException("Native Duel215 requires retained shared memory."), resources, nativeCrtRandom ?? throw new NotSupportedException("Native Duel215 requires its actual CRT random owner."));
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging.Write(NativeBattleDuel215Resources.ActorDirectory, resources.ReadActorDat().AsSpan());
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }

    private NativeBattleGraphicsState AdvanceNativeDuel215(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Duel215 requires retained effect memory.");
        NativeBattleDuel215Resources resources = memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 requires its actual entered resources.");
        NativeBattleEffectParameters parameters = memory.Duel215?.Parameters ?? throw new NotSupportedException("Native Duel215 requires its actual entered parameters.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Duel215 requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Duel215 requires its actual projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Duel215 requires its actual view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Duel215 requires its authenticated color arithmetic.");
        NativeBattleDuel215Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Duel215 requires its selected module."), NativeFileStaging ?? throw new NotSupportedException("Native Duel215 requires retained shared memory."), nativeCrtRandom ?? throw new NotSupportedException("Native Duel215 requires retained CRT state."));
        NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Native Duel215 requires its actual action contact pointer.");
        int count = header.ReadByte(16);
        ImmutableArray<byte> targets = Enumerable.Range(0, count).Select(index => nativeActionPackets.ReadContact(checked(first + index)).Primary.ActorIndex).ToImmutableArray();
        NativeBattleDuel215PreparationServices preparation = new(index => graphics.Models[index], WriteModel, Anchor, targets, Bind, Select);
        NativeBattleDuel215TargetServices recipients = new(index => graphics.Models[index], WriteModel, targets, Bounds, Body);
        NativeBattleDuel215Inputs inputs = new(Execute);
        NativeBattleDuel215SceneServices scene = new(index => graphics.Models[index], WriteModel, Anchor, (index, bone, fraction) => ReadNativeGraphicsBone(graphics, index, checked((byte)bone), checked((short)fraction)), targets, view, ExecuteScene);
        frame = NativeBattleDuel215Controller.Advance69AC00(frame, inputs, Child);
        Publish(frame);
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        void WriteModel(int index, NativeBattleGraphicsModel model) => graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        NativeBattleModuleAnchor Anchor(int index) => ReadNativeModelAnchor(graphics, index) with { W = (graphics.Models[index].Transform ?? throw new NotSupportedException("Native Duel215 requires its actual model WORD36.")).Angle36 };
        void Publish(NativeBattleDuel215Frame current)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            graphics = graphics with { OriginalEffect = current.Effect };
        }
        NativeBattleLogicalOrdering Ordering() => (graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel215 requires its actual ordering table.")).Pending;
        void StoreOrdering(NativeBattleLogicalOrdering ordering)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Duel215 requires its actual ordering table.");
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = ordering } };
        }
        NativeBattleDuel215Frame Bind(NativeBattleDuel215Frame current, int index, uint row)
        {
            Publish(current);
            NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[index], NativeGraphicsPoseResource(graphics, index));
            var bound = NativeBattleVictoryResources.Bind5022C0(resources.ReadActorDat(), NativeModelCpuResources ?? throw new NotSupportedException("Native Duel215 requires its actual CPU resource rows."), model, row, NativeBattleDuel215Resources.ActorDirectory, Read, (address, pointer) => current = current with { Effect = current.Effect with { Pools = NativeBattleEffectTaskMemory.WritePointer(current.Effect.Pools, address, pointer) } });
            WriteModel(index, bound.Model);
            return current;

            NativeBattleResourcePointer? Read(uint address)
            {
                if (NativeBattleEffectTaskMemory.TryReadPointer(current.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer;
                }
                return IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
            }
        }
        NativeBattleDuel215Frame Select(NativeBattleDuel215Frame current, int index, byte sequence)
        {
            Publish(current);
            graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            return current;
        }
        NativeBattleDuel215TargetBoundsResult Bounds(NativeBattleDuel215Frame current, int index)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[index];
            NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
            NativeBattleDuel215BoundsStep bounds = NativeBattleDuel215Bounds.Read69D360(current.Staging, model, resource.PoseSource ?? throw new NotSupportedException("Native Duel215 bounds require its actual selected skeleton."), DiablosActorGeometry(resource, model));
            WriteModel(index, bounds.Model);
            return new(current with { Staging = bounds.Staging }, bounds.Maximum, bounds.Minimum);
        }
        NativeBattleDuel215Frame Body(NativeBattleDuel215Frame current, int index, NativeBattleVector angles, NativeBattleVector pivot)
        {
            Publish(current);
            graphics = DrawNativeDuel215Body69D200(graphics, index, view, angles, pivot, projection);
            return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Duel215 body drawing lost its effect memory."), Staging = NativeFileStaging ?? throw new InvalidDataException("Native Duel215 body drawing lost its temporary memory.") };
        }
        NativeBattleDuel215Step Child(NativeBattleDuel215Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            if (pool.Slots[slot].Callback == 0x69B070)
            {
                return NativeBattleDuel215Controller.Advance69B070(current, pool, slot, scene);
            }
            if (pool.Slots[slot].Callback == 0x69CB10)
            {
                NativeBattleDuel215ParticleHierarchyStep advanced = NativeBattleDuel215Controller.Advance69CB10(current, pool, slot, view, projection, colors, Ordering());
                StoreOrdering(advanced.Ordering);
                return advanced.Step;
            }
            throw new NotSupportedException("Native Duel215 selected an unsupported registered callback.");
        }
        NativeBattleDuel215Frame Execute(NativeBattleDuel215Frame current, NativeBattleDuel215Operation operation)
        {
            switch (operation)
            {
                case NativeBattleDuel215PrepareScene prepare:
                    return NativeBattleDuel215Preparation.Initialize69AC00(current, prepare.Slot, preparation);
                case NativeBattleDuel215RestoreScene:
                    current = NativeBattleDuel215Preparation.RestoreModels69AC00(current, preparation);
                    NativeBattleCameraState active = NativeCamera ?? throw new NotSupportedException("Native Duel215 closure requires its actual camera owner.");
                    NativeBattleCameraPose captured = active.CapturedPose ?? throw new NotSupportedException("Native Duel215 closure requires its original camera capture.");
                    NativeCamera = active.WriteEffectGlobals(captured with { Roll = active.Roll }) with { EyePaddingF6 = active.CapturedEyePadding806, TargetPaddingFE = active.CapturedTargetPadding80E };
                    return current;
                case NativeBattleDuel215File file:
                    NativeBattleEffectFileAsset asset = resources.Files.Single(candidate => candidate.FileId == file.Request.FileId);
                    NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Duel215 reload requires its actual effect arena.")).Write(NativeBattleWaterExecutionFields.ArenaOffset(file.Request.DestinationAddress, asset.Data.Length), asset.Data.AsSpan());
                    NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Duel215 reload requires its actual IO scheduler.")).Queue482610(asset.Data.Length, file.Request.CompletionCallback, null);
                    return current with { Memory = current.Memory with { Arena = arena } };
                case NativeBattleDuel215Packets packets:
                    if (packets.Common)
                    {
                        NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Duel215 debris requires its current common packet owner."), Ordering(), packets.Batch);
                        StoreOrdering(common.Ordering);
                        return current with { Memory = current.Memory with { PrimitiveProcess = common.Process } };
                    }
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Duel215 lost its actual private packet cursor."), Ordering(), packets.Batch);
                    StoreOrdering(written.Ordering);
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor }, Staging = current.Staging.Write(0x2508988, BitConverter.GetBytes(checked(NativeBattleDuel215Controller.Arena + (uint)written.Cursor))) };
                default:
                    throw new NotSupportedException("Native Duel215 selected an unsupported original root operation.");
            }
        }
        NativeBattleDuel215Frame ExecuteScene(NativeBattleDuel215Frame current, NativeBattleDuel215SceneOperation operation)
        {
            switch (operation)
            {
                case NativeBattleDuel215Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native Duel215 requires its actual stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleDuel215StageFlags flags:
                    NativeBattleStageState groups = NativeStage ?? throw new NotSupportedException("Native Duel215 requires its actual stage rows.");
                    NativeStage = groups with { Groups = groups.Groups.Select(group => group with { Flags = flags.Enabled ? (byte)(group.Flags | 2) : (byte)(group.Flags & ~2) }).ToImmutableArray() };
                    return current;
                case NativeBattleDuel215ProgramDraw program:
                    NativeBattleDuel215WorkspaceStep drawn = NativeBattleDuel215Workspaces.Advance69B070(current, program.Slot, program.Offset, program.Size, program.Context, program.Anchored, projection, colors, Ordering());
                    StoreOrdering(drawn.Ordering);
                    return drawn.Frame;
                case NativeBattleDuel215BodyDraw body:
                    Publish(current);
                    graphics = DrawNativeDuel215Body69D100(graphics, body.Actor, view, projection);
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Duel215 actor drawing lost its effect memory."), Staging = NativeFileStaging ?? throw new InvalidDataException("Native Duel215 actor drawing lost its temporary memory.") };
                case NativeBattleDuel215TargetsDraw target:
                    return NativeBattleDuel215Targets.Advance69B070(current, target.Slot, target.Tick, recipients);
                case NativeBattleDuel215DebrisDraw debris:
                    NativeBattleModulePoolTask task = current.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel215Controller.Scene).Slots[debris.Slot];
                    return NativeBattleDuel215Controller.DrawParticles69CC90(current, task, inputs, view, projection);
                case NativeBattleDuel215Sound sound:
                    PublishNativeModuleSound(new(sound.Identifier, 0, null) { Pan = 128, RawPan = 128 });
                    return current;
                case NativeBattleDuel215Contact:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = Enumerable.Range(0, count).Select(index => nativeActionPackets.ReadContact(checked(first + index))).ToImmutableArray() });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Duel215 contact lost its actual effect memory.") };
                case NativeBattleDuel215Camera pose:
                    NativeCamera = NativeBattleDuel215Controller.AdvanceCamera69B070(NativeCamera ?? throw new NotSupportedException("Native Duel215 requires its actual camera owner."), pose.Tick, pose.OriginX, pose.OriginZ, pose.CenterX, pose.FarZ);
                    return current;
                default:
                    throw new NotSupportedException("Native Duel215 selected an unsupported original scene operation.");
            }
        }
    }
}
