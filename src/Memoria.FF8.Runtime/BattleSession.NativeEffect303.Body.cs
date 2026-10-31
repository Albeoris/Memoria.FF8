using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect303Frame Frame) DrawNativeEffect303Body(NativeBattleGraphicsState graphics, NativeBattleEffect303Frame frame, NativeBattleEffect303DrawBody188 operation)
    {
        uint delta = unchecked(operation.ModelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect303 body requires its actual physical recipient model.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePilotVisualIdentity? bodyIdentity = frame.PilotIdentity;
        int geometryPass = 0;
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Effect303 body requires its actual current recipient skeleton.");
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Effect303 body requires its actual loaded recipient geometry.");
        int scratchLength = checked(geometry.Max(mesh => mesh.Vertices.Length) * 12);
        if (scratchLength > NativeBattleEffect303Profile.BodyProjectionCapacity)
        {
            throw new NotSupportedException("Effect303 recipient projection leaves its current COMMON workspace.");
        }
        uint scratchAddress = NativeBattleEffect303Profile.ArenaAddress + NativeBattleEffect303Profile.ScratchOffset;
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect303 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect303 body requires its current projection.");
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect303 body requires its current temporary owner."), 188);
        NativeBattleEffect303BodyFrame body = new(frame.Memory, allocated.Staging, graphics.Models[actor], NativeBattleTemporaryMemory.ReadView(allocated.Staging, allocated.Address, 188), CommonCursor(frame.Memory), frame.State.ReadDword(NativeBattleEffect303Profile.CursorGlobal));
        body = NativeBattleEffect303BodyDrawing.DrawOuter(body, camera.CapturePackedView1D97778(), operation.TextureRectangle8, operation.Clut, operation.VOffset, operation.UOffset, operation.Color4, scratchAddress, new(Shadow, Compose, DrawSource, Restore));
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(body.Staging, allocated.Address, body.Temporary188, 188);
        frame = frame with { Memory = body.Memory, State = frame.State.WriteDword(NativeBattleEffect303Profile.CursorGlobal, body.FxCursor), Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 188) };
        return (graphics, frame);

        uint CommonCursor(NativeBattleEffectMemory memory) => checked((uint)(memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect303 body requires its current selected COMMON packet cursor.")));

        NativeBattleEffect303BodyFrame Deliver(NativeBattleEffect303BodyFrame current, NativeBattleGfGeometryBatch batch, bool common)
        {
            batch = NativeBattleEffect32Presentation.Depths(batch);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect303 body requires its current ordering table.");
            if (common)
            {
                NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect303 body requires its current ordinary COMMON owner.");
                AssertCommonCursor(current);
                NativeBattlePrimitiveProcess packetOwner = process;
                frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, packetOwner.PacketBank!, packetOwner.PacketBuffer, packetOwner.ByteCursor!.Value, batch) } };
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, batch);
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                NativeBattleEffectMemory memory = current.Memory with { PrimitiveProcess = written.Process };
                return current with { Memory = memory, CommonCursor = CommonCursor(memory) };
            }
            int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.FxCursor, 0);
            NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect303 body requires its current FX byte owner.");
            NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.Resolve(batch, cursor, NativeBattleEffectArena.Length, offset => arena.Read(offset, 1)[0]);
            frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, cursor, resolved) } };
            NativeBattleEffectPacketWrite effect = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = effect.Ordering } };
            return current with { Memory = effect.Memory, FxCursor = checked(NativeBattleEffect303Profile.ArenaAddress + (uint)effect.Cursor) };
        }

        void AssertCommonCursor(NativeBattleEffect303BodyFrame current)
        {
            if (current.CommonCursor != CommonCursor(current.Memory))
            {
                throw new InvalidDataException("Effect303 body lost its current selected COMMON cursor coordinate.");
            }
        }

        NativeBattleEffect303BodyFrame Shadow(NativeBattleEffect303BodyFrame current)
        {
            var shadowSource = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect303 shadow requires its original source.");
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect303 shadow requires its current matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect303 shadow requires its current arithmetic.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect303 shadow requires its current depth controls."), shadowSource, definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect303 shadow requires its actual primitive capabilities."), 4112, 16, bodyIdentity is { } shadowOwner ? shadowOwner with { ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(shadowSource), Primitive = unchecked(shadowOwner.Primitive * 397 + 0x5088) } : null);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = Deliver(current, shadow.Batch, true);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleEffect303BodyFrame Compose(NativeBattleEffect303BodyFrame current, NativeBattlePackedMatrix32 basis) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, basis.ReadMatrix()) };

        NativeBattleEffect303BodyFrame Restore(NativeBattleEffect303BodyFrame current)
        {
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, pose, NativeBattleTemporaryMemory.ReadView(temporary.Staging, temporary.Address, 88));
            NativeBattleFileStaging retained = NativeBattleTemporaryMemory.PublishView(temporary.Staging, temporary.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(retained, 88) };
        }

        NativeBattleEffect303BodyFrame DrawSource(NativeBattleEffect303BodyFrame current)
        {
            graphics = graphics with { Models = graphics.Models.SetItem(actor, current.Model) };
            NativeDeformedBodyBridge bridge = new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, NativeBattleEffectArena.Clear(), 0);
            NativeBattleResourcePointer source = NativeDeformedGeometryPointer(bridge, false);
            uint sourceAddress = source.Cpu?.PhysicalAddress ?? source.StagingAddress ?? throw new NotSupportedException("Effect303 body requires its actual addressed geometry.");
            NativeBattlePilotVisualIdentity? geometryIdentity = NativeBattleEffect32Presentation.Resource(bodyIdentity is { } owner ? owner with { ResourceGeneration = NativeBattleEffect32Presentation.ResourceEpoch(source) } : null, sourceAddress, geometryPass++);
            ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleBodyBoneMatrices.Require(current.Model, pose).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            uint startCommon = current.CommonCursor;
            uint startEffect = current.FxCursor;
            NativeBattleEffect303BodyStep drawn = NativeBattleEffect303BodyDrawing.DrawBodyWithPresentation(current.Memory, current.Temporary188, ReadGeometry, sourceAddress, bones, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect303 body requires its current projection controls."), scratchAddress, scratchLength, pilotIdentity: geometryIdentity);
            current = current with { Memory = drawn.Memory, Temporary188 = drawn.Temporary188 };
            foreach (NativeBattleEffect303BodyWrite write in drawn.Batch.Writes)
            {
                NativeBattleGfGeometryAttempt attempt = write.Attempt;
                if (write.Bank == NativeBattleEffect303BodyBank.ProjectionScratch)
                {
                    int address = checked(NativeBattleEffect303Profile.ScratchOffset + attempt.ByteOffsetRelative);
                    NativeBattleEffectArena arena = current.Memory.Arena!;
                    for (int offset = 0; offset < attempt.PacketLength; offset++)
                    {
                        arena = attempt.WriteMask[offset] switch
                        {
                            NativeBattleGfPacketByteState.Known => arena.Write(address + offset, [attempt.Data[offset]]),
                            NativeBattleGfPacketByteState.Unknown => arena.MarkUnknown(address + offset, 1),
                            NativeBattleGfPacketByteState.Untouched => arena,
                            _ => throw new InvalidDataException("Effect303 projection has an unsupported byte-store mask.")
                        };
                    }
                    current = current with { Memory = current.Memory with { Arena = arena } };
                    continue;
                }
                bool common = write.Bank == NativeBattleEffect303BodyBank.Common;
                uint expected = checked((common ? startCommon : startEffect) + (uint)attempt.ByteOffsetRelative);
                if (expected != (common ? current.CommonCursor : current.FxCursor))
                {
                    throw new InvalidDataException("Effect303 body packet stores lost their native ordered cursor.");
                }
                int allocation = attempt.Primitive is null ? 0 : attempt.PacketLength;
                current = Deliver(current, new([attempt with { ByteOffsetRelative = 0 }], allocation), common);
            }
            if (current.CommonCursor != startCommon + drawn.Batch.CommonAllocationBytes || current.FxCursor != startEffect + drawn.Batch.EffectAllocationBytes)
            {
                throw new InvalidDataException("Effect303 body cursor advances differ from its original native allocation.");
            }
            return current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, allocated.Address, current.Temporary188, 188) };

            NativeBattleEffectArena ReadGeometry(uint address, int length)
            {
                long offset = (long)address - sourceAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect303 body read leaves its actual current geometry.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, source, (int)offset, length));
            }
        }
    }
}
