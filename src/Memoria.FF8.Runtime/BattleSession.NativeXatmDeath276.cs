using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeXatmDeath276(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Native X-ATM092 requires its selected original module.");
        effect = effect with { PresentationPackets = [] };
        NativeBattleXatmDeath276Resources resources = effect.Binding.XatmDeath276Resources ?? throw new NotSupportedException("Native X-ATM092 requires its authenticated executable resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native X-ATM092 requires its retained effect memory.");
        int caster = effect.Parameters?.Actor78 ?? throw new NotSupportedException("Native X-ATM092 requires its actual caster parameter.");
        if (initialize)
        {
            NativeBattleXatmDeath276Frame initialized = NativeBattleXatmDeath276Controller.Initialize684F10(memory, effect, resources);
            NativeEffectMemory = initialized.Memory;
            NativeCamera = (NativeCamera ?? throw new NotSupportedException("Native X-ATM092 requires its actual camera.")).RegisterControlResource(initialized.Memory.Textures!.Animation);
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            return graphics with { Models = graphics.Models.SetItem(caster, actor with { Flags0 = (ushort)(actor.Flags0 & 0xFFFB) }), OriginalEffect = initialized.Effect with { XatmDeath276 = initialized.State } };
        }
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native X-ATM092 requires its published projection.");
        NativeBattleXatmDeath276State state = effect.XatmDeath276 ?? throw new NotSupportedException("Native X-ATM092 requires its executed684F10 state.");
        NativeBattleXatmDeath276Frame frame = new(memory, effect, state);
        NativeBattleXatmDeath276Inputs inputs = new(projection, () => graphics.Models[caster], ReadModelAddress, ReadBone, ReadVertexCount, ReadVertex, Execute, StartVibration, AdvanceArc, AdvancePrim) { CapturePresentation = true };
        frame = NativeBattleXatmDeath276Controller.Advance684FA0(frame, resources, inputs);
        NativeEffectMemory = frame.Memory;
        return graphics with { OriginalEffect = frame.Effect with { XatmDeath276 = frame.State } };

        NativeBattleGraphicsResource Resource() => NativeGraphicsPoseResource(graphics, caster);
        NativeBattlePoseSource Pose() => Resource().PoseSource ?? throw new NotSupportedException("Native X-ATM092 requires its actual caster rig.");
        ImmutableArray<NativeBattleRawMesh> Geometry() => Resource().Geometry ?? throw new NotSupportedException("Native X-ATM092 requires its actual caster geometry.");
        ImmutableArray<NativeBattleActionMatrix> Bones() => NativeBattleBodyBoneMatrices.Require(graphics.Models[caster], Pose());
        uint ReadModelAddress()
        {
            int row = graphics.Models[caster].ActiveResourceRow100 ?? throw new NotSupportedException("Native X-ATM092 requires its actual physical body resource row.");
            if ((uint)row >= 11)
            {
                throw new InvalidDataException("Native X-ATM092 body resource row leaves its original eleven slots.");
            }
            return checked(0x1D99774u + (uint)row * 0x34);
        }

        NativeBattleVector ReadBone(int bone, short fraction)
        {
            NativeBattlePoseSource pose = Pose();
            short parent = BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(16 + bone * 48));
            short distance = parent < 0 ? (short)0 : unchecked((short)(unchecked(BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(18 + parent * 48)) * fraction) >> 12));
            return NativeBattleGfMatrices.Transform460840(Bones()[bone], new(0, 0, distance));
        }

        int ReadVertexCount(int bone) => Geometry().Sum(mesh => mesh.Vertices.Count(vertex => vertex.Bone == bone));
        NativeBattleVector ReadVertex(int bone, int selected)
        {
            NativeBattleRawVertex vertex = Geometry().SelectMany(mesh => mesh.Vertices).Where(vertex => vertex.Bone == bone).ElementAt(selected);
            return NativeBattleGfMatrices.Transform460840(Bones()[bone], vertex.Position);
        }

        NativeBattleXatmDeath276Frame Execute(NativeBattleXatmDeath276Frame current, NativeBattleXatmDeath276Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleXatmDeath276Bones bones:
                    NativeBattleGraphicsModel actor = graphics.Models[caster];
                    actor = bones.World ? NativeBattleBodyBoneMatrices.Compose5095B0(actor, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native X-ATM092 requires its retained model matrix.")) : NativeBattleBodyBoneMatrices.Publish508C90(actor, Pose());
                    graphics = graphics with { Models = graphics.Models.SetItem(caster, actor) };
                    return current;
                case NativeBattleXatmDeath276Model model:
                    graphics = graphics with { Models = graphics.Models.SetItem(model.Model.Index, model.Model) };
                    return current;
                case NativeBattleXatmDeath276Caster body:
                    return DrawCaster(current, body.Hidden);
                case NativeBattleXatmDeath276InitializePrim prim:
                    (graphics, current) = InitializeNativeXatmDeath276Prim(graphics, current, prim.Slot);
                    return current;
                case NativeBattleXatmDeath276Burst burst:
                    NativeBattleGraphicsModel owner = graphics.Models[caster];
                    return NativeBattleXatmDeath276Debris.Burst687150(current, resources, burst, Geometry(), Bones(), owner.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native X-ATM092 requires its actual object mask."), Resource().GeometryRelocated507550 ? null : owner.BodyTextureSlots ?? throw new NotSupportedException("Native X-ATM092 requires its executed body material relocation."));
                case NativeBattleXatmDeath276Sprite sprite:
                    NativeBattleModulePacketBatch sprites = NativeBattleModulePackets.EmitSprite571DC0(sprite.Draw, projection, NativeCamera!.Roll, 17, 2);
                    sprites = sprites with { Primitives = sprites.Primitives.Select(item => item.Primitive is { PilotGeometry: { } visual } packet ? item with { Primitive = packet with { PilotGeometry = visual with { SourceDepths = new(packet.VertexPayload4[0], packet.VertexPayload4[1], packet.VertexPayload4[2], packet.VertexPayload4[3]), UseSourceDepths = true } } } : item).ToImmutableArray() };
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native X-ATM092 requires its physical ordering table.");
                    int cursor = current.State.DrawCursor ?? throw new NotSupportedException("Native X-ATM092 requires its selected packet arena.");
                    current = current with { Effect = current.Effect with { PresentationPackets = current.Effect.PresentationPackets.AddRange(sprites.Primitives.Select(item => new NativeBattlePrimitiveReference("20DFAB8", 0, checked(cursor + item.ByteOffset)))) } };
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, sprites, NativeBattleEffectGouraudWrites.FullDword);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State with { DrawCursor = written.Cursor } };
                case NativeBattleXatmDeath276Packets packets:
                    (graphics, current) = AppendNativeXatmDeath276Packets(graphics, current, packets.Batch);
                    return current;
                case NativeBattleXatmDeath276White white:
                    (graphics, current) = AppendNativeXatmDeath276Packets(graphics, current, CaptureWhite(NativeBattleXatmDeath276Body.White7040B0(255, 255, 255, white.Level), current), 1);
                    return current;
                case NativeBattleXatmDeath276Sound sound:
                    PublishNativeModuleSound(new(sound.Sound, 0, null) { Pan = 128 });
                    return current;
                case NativeBattleXatmDeath276Music music:
                    if ((ResolvedNativeFlags & 2) == 0)
                    {
                        ushort channel = NativeMusicProcess?.ReturnChannel ?? throw new NotSupportedException("Native47E3C0 requires its actual music return channel.");
                        NativeGlobalSoundProcess soundOwner = NativeGlobalSoundProcess ?? throw new NotSupportedException("Native47E3C0 requires its actual sound process.");
                        NativeMusicPlaybackState playback = soundOwner.MusicPlayback ?? throw new NotSupportedException("Native47E3C0 requires its current music playback.");
                        NativeGlobalSoundProcess = soundOwner with { MusicPlayback = playback.FadeGain46BBC0(unchecked((uint)(short)channel), unchecked((byte)music.Duration), 0) };
                    }
                    return current;
                case NativeBattleXatmDeath276StopVibration vibration:
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native X-ATM092 requires its actual vibration process.")).Stop4A2940(vibration.Handle);
                    return current;
                case NativeBattleXatmDeath276Fade fade:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(fade.Red, fade.Green, fade.Blue, checked((short)fade.Level)) };
                    return current;
                default:
                    throw new NotSupportedException("Native X-ATM092 operation requires its actual immediate owner.");
            }
        }

        static NativeBattleGfGeometryBatch CaptureWhite(NativeBattleGfGeometryBatch batch, NativeBattleXatmDeath276Frame current)
        {
            NativeBattlePilotVisualIdentity identity = NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(276, current.Effect.Pools[7].Slots[0], 500), 0x7040B0);
            return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = NativeBattleSelectedProgrammeVisuals.Discrete(primitive, identity) } : attempt).ToImmutableArray() };
        }

        NativeBattleXatmDeath276Vibration StartVibration(NativeBattleXatmDeath276Frame current)
        {
            NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native X-ATM092 requires its retained vibration process.")).Enqueue(resources.Vibration, 1, 128);
            NativeVibrationProcess = queued.State;
            return new(current, queued.Handle);
        }

        NativeBattleXatmDeath276Frame AdvancePrim(NativeBattleXatmDeath276Frame current, int slot)
        {
            (graphics, current) = AdvanceNativeXatmDeath276Prim(graphics, current, slot);
            return current;
        }

        NativeBattleXatmDeath276Step AdvanceArc(NativeBattleXatmDeath276Frame current, int slot)
        {
            NativeBattleEffectTaskPool pool = current.Effect.Pools[5];
            ImmutableArray<short> profile = Enumerable.Range(0, 8).Select(index => BinaryPrimitives.ReadInt16LittleEndian(current.Memory.Arena!.Read(current.State.BufferOffset + 0x4304 + index * 2, 2))).ToImmutableArray();
            ImmutableArray<ushort> lengths = Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(resources.ArcLengths.AsSpan().Slice(index * 2))).ToImmutableArray();
            NativeBattleSharedEffectRandomState random = current.Memory.SharedEffectRandom ?? throw new NotSupportedException("Native X-ATM092 arcs require their actual shared random owner.");
            uint? directionResidue = current.Memory.ExecutionStack is { } stack ? NativeBattleXatmDeath276Stack.ResolveDirectionResidue(stack, graphics.EntryLifecycle) : null;
            NativeBattleXatmDeath276ArcStep stepped = NativeBattleXatmDeath276Arcs.Advance(pool.Slots[slot], new(lengths, resources.ArcDistances), profile, projection, random, Triangle, directionResidue, NativeBattlePilotVisualLifetimes.Identity(276, pool.Slots[slot]));
            pool = pool.WriteSlot(slot, stepped.Task);
            current = current with { Memory = current.Memory with { SharedEffectRandom = stepped.Random }, Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(5, pool) } };
            (graphics, current) = AppendNativeXatmDeath276FramePackets(graphics, current, NativeBattleCinematicMonsterPackets.Pack(stepped.FramePrimitives), 1);
            return new(current, stepped.Result);

            NativeBattleQuezacotlRibbonSample Triangle(uint pointer)
            {
                NativeBattleXatmDeath276Table table = resources.Events.First(item => pointer >= item.TriangleAddress && pointer - item.TriangleAddress < item.Triangles.Length);
                int offset = checked((int)(pointer - table.TriangleAddress));
                if (offset % 24 != 0)
                {
                    throw new InvalidDataException("Native704130 triangle pointer does not identify its actual24-byte source record.");
                }
                return NativeBattleQuezacotlRibbonMath.Triangle6C9100(table.Triangles, offset / 24, bone => Bones()[bone], resources.TriangleLengths);
            }
        }

        NativeBattleXatmDeath276Frame DrawCaster(NativeBattleXatmDeath276Frame current, ImmutableArray<byte> hiddenList)
        {
            NativeEffectMemory = current.Memory;
            NativeBattleGraphicsModel actor = graphics.Models[caster];
            NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native685750 requires its actual view.");
            graphics = graphics with { OriginalEffect = current.Effect };
            graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 0x1010, false, callerPilotIdentity: NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(276, current.Effect.Pools[0].Slots[0], 1), ReadModelAddress()));
            current = current with { Effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native body shadow lost its selected effect owner.") };
            actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, NativeBattleGfMatrices.Compose56C2F0(view, actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native685750 requires its actual model matrix.")));
            graphics = graphics with { Models = graphics.Models.SetItem(caster, actor) };
            current = current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native685750 shadow lost process memory.") };
            HashSet<int> hidden = hiddenList.Where(bone => bone != 255).Select(bone => (int)bone).ToHashSet();
            uint mask = actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native685750 requires its actual object mask.");
            ImmutableArray<NativeBattleRawMesh> meshes = Geometry();
            for (int index = 0; index < meshes.Length; index++)
            {
                if ((mask & (1u << (index & 31))) == 0)
                {
                    continue;
                }
                NativeBattleRawMesh mesh = meshes[index];
                ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native685750 requires its actual shared projected scratch capacity.");
                uint address = checked(0x196B018u - (uint)maximum * 8);
                NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native685750 requires its retained shared projection bank.");
                NativeBattleXatmDeath276BodyProjection projected = NativeBattleXatmDeath276Body.Project6A2A90(mesh, Bones(), projection, staging.Read(address, mesh.Vertices.Length * 8).ToImmutableArray(), hidden, true);
                NativeFileStaging = staging.Write(address, projected.Bytes.AsSpan());
                NativeBattleScriptedGraphicsMemory registers = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native685750 requires its current GTE depth registers.");
                NativeBattleGfGeometryBatch batch = NativeBattleXatmDeath276Body.CapturedPackets6859D0(mesh.Polygons, projected, actor.Color40 ?? throw new NotSupportedException("Native685750 requires its entity color DWORD40."), Resource().GeometryRelocated507550 ? null : actor.BodyTextureSlots ?? throw new NotSupportedException("Native685750 requires body materials."), registers.Zsf3 ?? throw new NotSupportedException("Native685750 requires ZSF3."), registers.Zsf4 ?? throw new NotSupportedException("Native685750 requires ZSF4."), NativeBattleSelectedProgrammeVisuals.Resource(NativeBattlePilotVisualLifetimes.Identity(276, current.Effect.Pools[0].Slots[0], index + 10), ReadModelAddress(), checked((uint)index)));
                (graphics, current) = AppendNativeXatmDeath276FramePackets(graphics, current, batch);
            }
            actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[caster], Pose());
            graphics = graphics with { Models = graphics.Models.SetItem(caster, actor) };
            return current;
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleXatmDeath276Frame Frame) AppendNativeXatmDeath276Packets(NativeBattleGraphicsState graphics, NativeBattleXatmDeath276Frame frame, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native X-ATM092 requires its physical ordering table.");
        int cursor = frame.State.DrawCursor ?? throw new NotSupportedException("Native X-ATM092 requires its selected module arena.");
        frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, cursor, batch) } };
        batch = NativeBattleCinematicActorPresentation.NativeDepths(batch);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, primitive.Pending, batch, flags20);
        NativeEffectMemory = written.Memory;
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, frame with { Memory = written.Memory, State = frame.State with { DrawCursor = written.Cursor } });
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleXatmDeath276Frame Frame) AppendNativeXatmDeath276FramePackets(NativeBattleGraphicsState graphics, NativeBattleXatmDeath276Frame frame, NativeBattleGfGeometryBatch batch, ushort flags20 = 0)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native X-ATM092 requires its physical frame ordering table.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native X-ATM092 requires its actual FRAME packet arena.");
        frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
        batch = NativeBattleCinematicActorPresentation.NativeDepths(batch);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch, flags20);
        NativeEffectMemory = frame.Memory with { PrimitiveProcess = written.Process };
        return (graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } }, frame with { Memory = NativeEffectMemory });
    }
}

