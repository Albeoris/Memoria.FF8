using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState EnterNativeBlastingZone164(NativeBattleGraphicsState graphics, NativeBattleBlastingZone164Resources resources)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Blasting Zone requires its selected original module.");
        NativeBattleEffectParameters parameters = effect.Parameters ?? throw new InvalidDataException("Native Blasting Zone requires its entered parameters.");
        NativeBattleGraphicsModel model = graphics.Models[parameters.Actor78];
        NativeBattleVector rotation = new(model.Rotation12 ?? throw new NotSupportedException("Native Blasting Zone requires actor rotation X."), model.Rotation14 ?? throw new NotSupportedException("Native Blasting Zone requires actor rotation Y."), model.Rotation16 ?? throw new NotSupportedException("Native Blasting Zone requires actor rotation Z."));
        NativeBattleVector bone = ReadNativeGraphicsBone(graphics, parameters.Actor78, 241, 0);
        NativeBattleVector center = NativeBattleActionGeometryRules.TargetCenter(Enumerable.Range(0, 7).Where(actor => (parameters.TargetMask7A & (1 << actor)) != 0).Select(actor => graphics.Models[actor].Transform?.Position28 ?? throw new NotSupportedException("Native Blasting Zone requires actual target positions.")).ToArray());
        NativeBattleBlastingZone164EntryStep entry = NativeBattleBlastingZone164Controller.Enter5F0260(NativeEffectMemory ?? throw new NotSupportedException("Native Blasting Zone requires its retained process memory."), effect, NativeFileStaging ?? NativeBattleFileStaging.Unknown(), nativeActionPackets, resources, nativeCrtRandom ?? throw new NotSupportedException("Native Blasting Zone requires its retained CRT random owner."), rotation, bone, center, model.RotationPadding18);
        NativeEffectMemory = entry.Frame.Memory;
        NativeFileStaging = entry.Frame.Staging;
        nativeCrtRandom = entry.Frame.Random;
        return graphics with { OriginalEffect = entry.Frame.Effect, Models = graphics.Models.SetItem(parameters.Actor78, model with { Rotation14 = entry.Facing14 }) };
    }

    private NativeBattleGraphicsState AdvanceNativeBlastingZone164(NativeBattleGraphicsState graphics)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Blasting Zone requires its retained process memory.");
        NativeBattleBlastingZone164Resources resources = memory.BlastingZone164?.RetainedResources ?? throw new NotSupportedException("Native Blasting Zone requires resources retained by actual entry.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Native Blasting Zone requires its actual camera owner.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Native Blasting Zone requires its actual projection registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Blasting Zone requires its published view matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Blasting Zone requires its original color arithmetic.");
        NativeBattleBlastingZone164Frame frame = new(memory, graphics.OriginalEffect ?? throw new InvalidDataException("Native Blasting Zone requires its entered module."), NativeFileStaging ?? throw new NotSupportedException("Native Blasting Zone requires its entered physical resource bytes."), nativeCrtRandom ?? throw new NotSupportedException("Native Blasting Zone requires its retained CRT random owner."));
        NativeBattleBlastingZone164Inputs inputs = new(graphics.Flags, () => NativeEffectIoResult ?? throw new NotSupportedException("Native Blasting Zone requires its signed file IO result."), actor => graphics.Models[actor].Transform?.Scale38 ?? throw new NotSupportedException("Native Blasting Zone requires actual actor scale."), actor => ReadNativeRenzokuken160WeaponTip(graphics, actor), Execute);
        frame = NativeBattleBlastingZone164Controller.Advance5F5B10(frame, Child);
        NativeEffectMemory = frame.Memory;
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleBlastingZone164ChildStep Child(NativeBattleBlastingZone164Frame current, NativeBattleEffectTaskPool pool, int slot)
        {
            return pool.Slots[slot].Callback switch
            {
                0x5F0430 => NativeBattleBlastingZone164Controller.Advance5F0430(current, pool, slot, inputs, view, colors),
                0x63E9C0 => NativeBattleBlastingZone164Controller.AdvanceCamera63E9C0(current, pool, slot, graphics.Flags, (NativeCamera ?? throw new NotSupportedException("Native Blasting Zone requires its actual camera owner.")).CurrentPose ?? throw new NotSupportedException("Native Blasting Zone requires its current camera pose."), PublishCamera),
                0x5F0B70 or 0x5F0D10 or 0x5F1E70 or 0x5F1F80 or 0x5F2120 or 0x5F3B30 or 0x5F3FA0 => NativeBattleBlastingZone164Controller.AdvanceBasicMesh(current, pool, slot, inputs, view),
                0x5F1500 or 0x5F3DF0 or 0x5F4180 or 0x5F44F0 or 0x5F49B0 or 0x5F4B70 or 0x5F4FA0 or 0x5F5430 or 0x5F55D0 => NativeBattleBlastingZone164Controller.AdvanceOtherMesh(current, pool, slot, inputs, view, unchecked((short)(projection.FocalLength ?? throw new NotSupportedException("Native Blasting Zone requires its GTE focal length.")))),
                0x5F4720 => NativeBattleBlastingZone164Controller.Advance5F4720(current, pool, slot, inputs, view),
                0x5F16A0 => NativeBattleBlastingZone164Controller.Advance5F16A0(current, pool, slot, inputs),
                0x5F1800 or 0x5F1970 => NativeBattleBlastingZone164Controller.AdvanceExpandingMesh(current, pool, slot, inputs, view),
                0x5F1190 => NativeBattleBlastingZone164Controller.Advance5F1190(current, pool, slot, inputs, view),
                0x5F1AC0 => NativeBattleBlastingZone164Controller.Advance5F1AC0(current, pool, slot, inputs, view),
                0x5F0E80 or 0x5F5120 or 0x5F57C0 => NativeBattleBlastingZone164Controller.AdvanceSpriteParticles(current, pool, slot, inputs, view, projection, camera.Roll),
                0x5F4330 => NativeBattleBlastingZone164Controller.Advance5F4330(current, pool, slot, inputs),
                0x5F4CC0 => NativeBattleBlastingZone164Controller.Advance5F4CC0(current, pool, slot, inputs, projection),
                0x5F5A50 => NativeBattleBlastingZone164Controller.Advance5F5A50(current, pool, slot, inputs),
                0x5F5AD0 => NativeBattleBlastingZone164Controller.Advance5F5AD0(current, pool, slot, TextureLoad),
                uint callback => throw new NotSupportedException($"Native Blasting Zone callback0x{callback:X} has no original implementation.")
            };
        }

        NativeBattleBlastingZone164Frame PublishCamera(NativeBattleBlastingZone164Frame current, NativeBattleGuardianCameraStep step)
        {
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Blasting Zone requires its current camera owner.")).WriteEffectGlobals(step.Pose);
            return current;
        }

        NativeBattleBlastingZone164Frame Execute(NativeBattleBlastingZone164Frame current, NativeBattleBlastingZone164Operation operation)
        {
            NativeEffectMemory = current.Memory;
            NativeFileStaging = current.Staging;
            switch (operation)
            {
                case NativeBattleBlastingZone164BindActor:
                    byte actor = current.Parameters.Actor78;
                    NativeBattleGraphicsModel model = RetainNativeDecodedPoseBeforeResourceChange(graphics.Models[actor], NativeGraphicsPoseResource(graphics, actor));
                    NativeBattleVictoryResourceBinding bound = NativeBattleVictoryResources.Bind5022C0(current.Staging, NativeModelCpuResources ?? throw new NotSupportedException("Native Blasting Zone requires its actual CPU resource rows."), model, 0x23C8EF8, 0xD88AA0);
                    NativeFileStaging = bound.Staging;
                    graphics = graphics with { Models = graphics.Models.SetItem(actor, bound.Model) };
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleBlastingZone164ForceCamera:
                    NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native Blasting Zone requires its actual camera owner.")).Apply50A730();
                    return current;
                case NativeBattleBlastingZone164Sequence sequence:
                    graphics = SelectNativeHeaderSequence(graphics, sequence.Actor, sequence.Sequence);
                    return current with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
                case NativeBattleBlastingZone164ModelFlags flags:
                    NativeBattleGraphicsModel changed = graphics.Models[flags.Actor];
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, changed with { Flags0 = unchecked((ushort)((changed.Flags0 | flags.Set) & ~flags.Clear)) }) };
                    return current;
                case NativeBattleBlastingZone164StageFlags flags:
                    NativeBattleStageState stageFlags = NativeStage ?? throw new NotSupportedException("Native Blasting Zone requires its actual stage groups.");
                    NativeStage = stageFlags with { Groups = stageFlags.Groups.Select(group => group with { Flags = unchecked((byte)(flags.Set ? group.Flags | 2 : group.Flags & ~2)) }).ToImmutableArray() };
                    return current;
                case NativeBattleBlastingZone164Sound sound:
                    PublishNativeModuleSound(new(sound.Value, 0, null) { Pan = 128, RawPan = 128 });
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlastingZone164ReadTexture read:
                    NativeBattleEffectFileAsset file = resources.Files.Single(file => file.FileId == read.FileId);
                    NativeEffectIoResult = -1;
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Blasting Zone requires its current effect arena.");
                    current = current with { Memory = current.Memory with { Arena = arena.Write(0x10000, file.Data.AsSpan()) } };
                    NativeEffectMemory = current.Memory;
                    QueueNativeFileIo482610(file.Data.Length, 0x508470);
                    return current;
                case NativeBattleBlastingZone164EnqueueTexture:
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Blasting Zone requires its actual texture queue.");
                    (textures, _) = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Blasting Zone requires actual loaded texture bytes."), 0x10000);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleBlastingZone164Flash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.Mode), unchecked((short)flash.First), unchecked((short)flash.Last), unchecked((short)flash.Intensity)) };
                    return current;
                case NativeBattleBlastingZone164Contacts:
                    graphics = ApplyNativeHeaderContacts(graphics, current.Parameters.HeaderIndex7C);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleBlastingZone164Stage stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Native Blasting Zone requires its actual stage rows.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Intensity), ColorFlags = 0 }).ToImmutableArray() };
                    return current;
                case NativeBattleBlastingZone164Shake shake:
                    NativeBattleCameraState shaking = NativeCamera ?? throw new NotSupportedException("Native Blasting Zone requires its current camera owner.");
                    NativeCamera = shaking with { ViewTranslationScratch = (shaking.ViewTranslationScratch ?? throw new NotSupportedException("Native Blasting Zone requires its retained translation scratch.")) with { Y = shake.Offset } };
                    return current;
                case NativeBattleBlastingZone164MeshDraw mesh:
                    NativeBattleStaticMeshResource resource = resources.Meshes.Single(resource => resource.Address == mesh.Address);
                    Func<int, NativeBattleVector>? vertices = mesh.SelectedVerticesAddress is uint selected ? offset => NativeBattleBlastingZone164Packets.ReadSelectedVertex(current.Staging, selected, offset) : null;
                    NativeBattleGfGeometryBatch batch;
                    if (mesh.Scrolling is { } scrolling)
                    {
                        NativeBattleScriptedGraphicsMemory registers = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Blasting Zone requires its actual GTE registers.");
                        batch = NativeBattleShivaScrollingPackets.Emit64E080(resource, mesh.Matrix, projection, scrolling, registers.Zsf3 ?? throw new NotSupportedException("Native Blasting Zone requires its GTE ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native Blasting Zone requires its GTE ZSF4."), registers.Dqa, registers.Dqb, colors);
                    }
                    else
                    {
                        batch = mesh.Tinted ? NativeBattleTintedMeshPackets.Emit(resource.Bytes, mesh.Matrix, projection, mesh.Context, unchecked((uint)ReadInt(current.Staging, 0x23CA110)), colors, vertices) : NativeBattleModulePackets.EmitBatch572200(resource.Bytes, 0, default, mesh.Matrix, projection, mesh.Context, colors, vertices);
                    }
                    return AppendGeometry(current, batch, mesh.Common);
                case NativeBattleBlastingZone164Sprite sprite:
                    return AppendPackets(current, sprite.Batch, sprite.Common);
                case NativeBattleBlastingZone164Tiles tiles:
                    return AppendPackets(current, tiles.Batch, true);
                default:
                    throw new NotSupportedException("Native Blasting Zone service has no original implementation.");
            }
        }

        NativeBattleBlastingZone164Frame AppendGeometry(NativeBattleBlastingZone164Frame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Blasting Zone requires its initialized ordering table.");
            if (common)
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Blasting Zone requires its common packet owner."), primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
            }
            NativeBattleEffectPacketWrite effectWritten = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new InvalidDataException("Native Blasting Zone lost its actual private cursor."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = effectWritten.Ordering } };
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked(0x20DFAB8u + (uint)effectWritten.Cursor));
            return current with { Memory = effectWritten.Memory, Effect = current.Effect with { DrawArenaOffset = effectWritten.Cursor }, Staging = current.Staging.Write(0x23CA118, bytes) };
        }

        NativeBattleBlastingZone164Frame AppendPackets(NativeBattleBlastingZone164Frame current, NativeBattleModulePacketBatch batch, bool common)
        {
            ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            foreach (NativeBattleAllocatedPrimitive packet in batch.Primitives)
            {
                byte[] data = new byte[packet.Primitive.Words.Length * 4];
                for (int index = 0; index < packet.Primitive.Words.Length; index++)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4, 4), packet.Primitive.Words[index]);
                }
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, data.Length).ToImmutableArray();
                attempts.Add(new(packet.ByteOffset, data.ToImmutableArray(), mask, packet.Primitive with { PhysicalWriteMask = mask }));
            }
            return AppendGeometry(current, batch.CapturedWrites ?? new(attempts.ToImmutable(), batch.AllocationBytes), common);
        }

        NativeBattleBlastingZone164Frame TextureLoad(NativeBattleBlastingZone164Frame current, uint completion)
        {
            (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(0x20DFAB8, completion, (address, value) => current = current with { Effect = NativeBattleBlastingZone164Controller.WriteCompletionByte(current.Effect, address, value) });
            graphics = graphics with { Auxiliary = auxiliary };
            return current;
        }

        static int ReadInt(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadInt32LittleEndian(staging.Read(address, 4));
    }

    private bool TryWriteNativeBlastingZone164Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x23C8F50);
        if (delta >= 100 * 36 || delta % 36 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5F0260 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleBlastingZone164Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5F0260 } current ? current : throw new NotSupportedException("Native Blasting Zone completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleBlastingZone164Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
