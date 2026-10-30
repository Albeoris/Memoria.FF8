using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleRayBomb307Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeRayBomb307(NativeBattleGraphicsState graphics) => RunNativeRayBomb307(graphics, true);
    private NativeBattleGraphicsState AdvanceNativeRayBomb307(NativeBattleGraphicsState graphics) => RunNativeRayBomb307(graphics, false);

    private NativeBattleGraphicsState RunNativeRayBomb307(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Ray-Bomb requires its selected original effect.");
        NativeBattleRayBomb307Resources resources = effect.Binding.RayBomb307Resources ?? throw new NotSupportedException("Ray-Bomb requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Ray-Bomb requires its process memory.");
        NativeBattleRayBomb307Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Ray-Bomb requires its CRT stream."), memory.RayBomb307 ?? throw new NotSupportedException("Ray-Bomb requires its retained process banks.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Ray-Bomb requires its addressed temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Ray-Bomb requires its original interpolation constants.");
        NativeBattleRayBomb307Inputs inputs = new(nativeActionPackets, graphics.Flags, actor => graphics.Models[actor], ReadBoneMatrix, TargetCenter, Anchor, actor => ReadModel8(actor, 0x0C), actor => ReadModel8(actor, 0x1C), Execute) { CapturePresentation = true, ReadView = _ => Camera().CapturePackedView1D97778(), ReadCameraEye = _ => Camera().CurrentPose?.Eye is { } eye ? new(eye.X, eye.Y, eye.Z) : throw new NotSupportedException("Ray-Bomb requires its current camera eye.") };
        frame = initialize ? NativeBattleRayBomb307Controller.Initialize(frame, resources, inputs) : NativeBattleRayBomb307Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Ray-Bomb requires its current camera.");

        void Publish(NativeBattleRayBomb307Frame current)
        {
            NativeEffectMemory = current.Memory with { RayBomb307 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Ray-Bomb lost its addressed memory.");
            nativeCrtRandom = current.Random;
            graphics = graphics with { OriginalEffect = current.Effect };
        }

        NativeBattleRayBomb307Frame Import(NativeBattleRayBomb307Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Ray-Bomb external service lost its process memory.");
            return current with { Memory = latest, State = latest.RayBomb307 ?? throw new InvalidDataException("Ray-Bomb lost its process banks."), Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Ray-Bomb lost its current tasks."), Staging = NativeFileStaging, Random = nativeCrtRandom ?? throw new InvalidDataException("Ray-Bomb lost its CRT stream.") };
        }

        NativeBattleEffectArena ReadModel8(int actor, int offset) => NativeBattleRaldo19BallController.Copy(NativeBattleRaldo19ModelMemory.Capture(graphics.Models[actor]), offset, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);

        NativeBattlePackedMatrix32 ReadBoneMatrix(NativeBattleRayBomb307Frame current, int actor, int selector)
        {
            Publish(current);
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Ray-Bomb requires its current caster skeleton.");
            int bone = selector;
            if (selector >= 0xF0)
            {
                int alias = selector - 0xEC;
                if ((uint)alias >= 16)
                {
                    throw new InvalidDataException("Ray-Bomb selected a bone alias outside its actual skeleton header.");
                }
                bone = source.Skeleton[alias];
            }
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], source);
            if ((uint)bone >= matrices.Length)
            {
                throw new InvalidDataException("Ray-Bomb selected a matrix outside its current skeleton.");
            }
            return NativeBattlePackedMatrix32.FromMatrix(matrices[bone], null);
        }

        NativeBattleRayBomb307PointStep TargetCenter(NativeBattleRayBomb307Frame current, ushort mask)
        {
            NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (mask & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Ray-Bomb requires its selected physical target positions.")).ToArray());
            return new(current, center);
        }

        NativeBattleRayBomb307AnchorStep Anchor(NativeBattleRayBomb307Frame current, int actor)
        {
            Publish(current);
            NativeBattleGraphicsModel model = graphics.Models[actor];
            if ((model.Flags0 & 2) != 0)
            {
                NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Ray-Bomb requires its current actor skeleton.");
                foreach (byte selector in new byte[] { 0xF0, 0xF1 })
                {
                    NativeFileStaging = current.Staging;
                    current = current with { Memory = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0)), Staging = NativeFileStaging };
                }
            }
            NativeBattleModuleAnchor anchor = ReadNativeModelAnchor(graphics, actor);
            return new(current, anchor with { W = (model.Transform ?? throw new NotSupportedException("Ray-Bomb requires its current actor heading.")).Angle36 });
        }

        NativeBattleRayBomb307Frame Deliver(NativeBattleRayBomb307Frame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleRayBomb307Presentation.PreserveNativeDepths(batch);
            uint cursor = current.State.ReadDword(CursorGlobal);
            uint working = current.State.ReadDword(WorkingGlobal);
            NativeBattleModulePoolTask root = current.Effect.Pools[0].Slots.Single(task => (task.Flags & 1) != 0 && task.Callback == RootCallback);
            uint bank = checked(working + ((root.ReadWord(12) & 1) == 0 ? 0u : PacketBankLength));
            if (cursor < bank || cursor > bank + PacketBankLength || batch.AllocationBytes < 0 || batch.AllocationBytes > bank + PacketBankLength - cursor)
            {
                throw new InvalidDataException("Ray-Bomb drawing leaves its selected physical packet bank.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Ray-Bomb requires its current ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(CursorGlobal, checked(ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleRayBomb307Frame Execute(NativeBattleRayBomb307Frame current, NativeBattleRayBomb307Operation operation)
        {
            Publish(current);
            switch (operation)
            {
                case NativeBattleRayBomb307BindCaster bind:
                    if (bind.ResourceAddress != CasterDatAddress || bind.SaveAddress != SavedBindingAddress)
                    {
                        throw new InvalidDataException("Ray-Bomb caster binding leaves its original resource directory and saved row.");
                    }
                    NativeBattleGraphicsModel boundActor = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[bind.Actor], NativeGraphicsPoseResource(graphics, bind.Actor));
                    NativeBattleFileStaging staging = current.Staging!.Write(bind.ResourceAddress, resources.CasterDat.Data.AsSpan());
                    var bound = NativeBattleVictoryResources.Bind5022C0(resources.CasterDat.Data, NativeModelCpuResources ?? throw new NotSupportedException("Ray-Bomb requires its current CPU resource rows."), boundActor, bind.SaveAddress, bind.ResourceAddress, ReadPointer, WritePointer);
                    graphics = graphics with { Models = graphics.Models.SetItem(bind.Actor, bound.Model) };
                    return current with { Staging = staging };

                    NativeBattleResourcePointer? ReadPointer(uint address) => address >= SavedBindingAddress && address < SavedBindingAddress + 32 ? current.State.ReadResourcePointer(address) : staging.ReadResourcePointer(address);

                    void WritePointer(uint address, NativeBattleResourcePointer? pointer)
                    {
                        current = current with { State = current.State.WriteResourcePointer(address, pointer) };
                    }
                case NativeBattleRayBomb307RingSequence sequence:
                    graphics = ScheduleNativeRingSequence505C00(graphics, sequence.Actor, sequence.Sequence);
                    return Import(current);
                case NativeBattleRayBomb307ActorFlags flags:
                    NativeBattleGraphicsModel flagged = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, flagged with { Flags0 = unchecked((ushort)((flagged.Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleRayBomb307ActorWrite8 write:
                    NativeBattleGraphicsModel actor = graphics.Models[write.Actor];
                    if (write.Rotation8 is { } rotation)
                    {
                        actor = NativeBattleRaldo19ModelMemory.Apply(actor, 0x0C, rotation, 0, 4);
                        actor = NativeBattleRaldo19ModelMemory.Apply(actor, 0x10, rotation, 4, 4);
                    }
                    if (write.Position8 is { } position)
                    {
                        actor = NativeBattleRaldo19ModelMemory.Apply(actor, 0x1C, position, 0, 4);
                        actor = NativeBattleRaldo19ModelMemory.Apply(actor, 0x20, position, 4, 4);
                    }
                    graphics = graphics with { Models = graphics.Models.SetItem(write.Actor, actor) };
                    return current;
                case NativeBattleRayBomb307StageFade fade:
                    NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Ray-Bomb requires its current stage color rows.");
                    NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = fade.Depth, ColorFlags = fade.ColorFlags }).ToImmutableArray() };
                    return current;
                case NativeBattleRayBomb307SpatialSound sound:
                    if (!resources.Sounds.Any(resource => resource.Address == sound.DescriptorAddress && resource.Value == sound.Value))
                    {
                        throw new InvalidDataException("Ray-Bomb sound differs from its original descriptor.");
                    }
                    byte[] coordinates = sound.Position8.Read(0, 6);
                    PublishNativeModuleSound(sound.Value, sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(coordinates), BinaryPrimitives.ReadInt16LittleEndian(coordinates.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(coordinates.AsSpan(4)), 0));
                    return Import(current);
                case NativeBattleRayBomb307Contact contact:
                    NativeBattleActionHeaderSlot header = nativeActionPackets.Headers[current.Effect.Parameters!.HeaderIndex7C];
                    if ((uint)contact.RelativeContactIndex >= header.ReadByte(16))
                    {
                        throw new InvalidDataException("Ray-Bomb selected a contact outside its captured action header.");
                    }
                    int first = header.FirstContactIndex ?? throw new NotSupportedException("Ray-Bomb requires its captured physical contact pointer.");
                    NativeBattleContactPacket selected = nativeActionPackets.ReadContact(checked(first + contact.RelativeContactIndex));
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [selected] });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleRayBomb307RestoreTexture restore:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(restore.Destination, restore.CompletionAddress, (address, value) => current = current with { Effect = WriteCompletion(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                case NativeBattleRayBomb307DrawMesh88 draw:
                    NativeBattleEffectArena context = View(current.Staging!, draw.TemporaryAddress, 88);
                    NativeBattleGeometryRegisters registers = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Ray-Bomb requires its current drawing matrix.");
                    NativeBattleStaticMeshContext88Step mesh = NativeBattleStaticMeshContext88.Draw(current.Memory, resources.ReadMesh(draw.SourceAddress), NativeBattleGeometry.CaptureMatrix(registers), Camera().Projection!, context, colors, registers.Rotation20[18] is byte low && registers.Rotation20[19] is byte high ? unchecked((short)(low | high << 8)) : null, pilotIdentity: draw.PilotIdentity);
                    return Deliver(current with { Memory = mesh.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, draw.TemporaryAddress, mesh.Context, 88) }, mesh.Batch);
                case NativeBattleRayBomb307DrawSprite180 draw:
                    NativeBattleEffectArena temporary = View(current.Staging!, draw.TemporaryAddress, 180);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != draw.SourceAddress || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(4, 2)) != draw.Phase || BinaryPrimitives.ReadUInt16LittleEndian(temporary.Read(0x24, 2)) != draw.Flags)
                    {
                        throw new InvalidDataException("Ray-Bomb sprite arguments differ from its current physical context.");
                    }
                    NativeBattleModuleSpriteDraw sprite = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, resources.ReadSprite, Camera().Projection!, Camera().Roll, pilotIdentity: draw.PilotIdentity);
                    return Deliver(current with { Memory = sprite.Memory, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging!, draw.TemporaryAddress, sprite.Temporary180, 180) }, sprite.Batch);
                default:
                    throw new NotSupportedException("Ray-Bomb operation requires its original native implementation.");
            }
        }

        NativeBattleOriginalEffectState WriteCompletion(NativeBattleOriginalEffectState owner, uint address, byte value)
        {
            uint relative = unchecked(address - RootBank);
            if (relative >= 32 || relative % 16 != 14)
            {
                throw new InvalidDataException("Ray-Bomb completion leaves its actual root pool.");
            }
            NativeBattleModulePoolTask task = NativeBattleRayBomb307Controller.Read(currentFrame(owner), address - 14);
            if (task.Callback != TextureCallback)
            {
                throw new InvalidDataException("Ray-Bomb completion requires its actual texture callback.");
            }
            return NativeBattleRayBomb307Controller.Write(currentFrame(owner), address - 14, task.WriteByte(14, value)).Effect;

            NativeBattleRayBomb307Frame currentFrame(NativeBattleOriginalEffectState selected) => frame with { Effect = selected };
        }

        static NativeBattleEffectArena View(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
