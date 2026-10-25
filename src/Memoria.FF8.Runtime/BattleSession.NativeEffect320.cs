using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeEffect320(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Effect320 requires its selected original effect.");
        NativeBattleEffect320Resources resources = effect.Binding.Effect320Resources ?? throw new NotSupportedException("Effect320 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect320 requires its current process memory.");
        NativeBattleEffect320Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect320 requires its current CRT stream."), memory.Effect320 ?? throw new NotSupportedException("Effect320 requires its retained storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Effect320 requires its current addressed memory.") };
        NativeBattleEffect320Inputs inputs = new(definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect320 requires its arithmetic constants."), _ => nativeActionPackets, (_, actor) => graphics.Models[actor], (_, actor) => CaptureNativeEffect320Model(graphics.Models[actor]), _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Effect320 requires its current projection."), _ => Camera().Roll, Anchor571400, Origin65DA30, Execute);
        frame = initialize ? NativeBattleEffect320Controller.Initialize(frame, resources, inputs) : NativeBattleEffect320Controller.Advance(frame, resources, inputs);
        Publish(frame);
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Effect320 requires its current camera.");

        void Publish(NativeBattleEffect320Frame current)
        {
            NativeEffectMemory = current.Memory with { Effect320 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Effect320 lost its current addressed memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleEffect320Frame Import(NativeBattleEffect320Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Effect320 service lost its memory.");
            return current with { Memory = latest, State = latest.Effect320 ?? throw new InvalidDataException("Effect320 service lost its storage."), Random = nativeCrtRandom ?? throw new InvalidDataException("Effect320 service lost its CRT stream."), Staging = NativeFileStaging ?? throw new InvalidDataException("Effect320 service lost its addressed memory.") };
        }

        NativeBattleEffect320PointStep Anchor571400(NativeBattleEffect320Frame current, int actor, NativeBattleEffectArena retained)
        {
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect320 requires its current actor transform.");
            if ((model.Flags0 & 2) == 0)
            {
                return new(current, retained.Write(0, BitConverter.GetBytes(transform.Position28.X)).Write(2, BitConverter.GetBytes(transform.Position28.Y)).Write(4, BitConverter.GetBytes(transform.Position28.Z)).Write(6, BitConverter.GetBytes(transform.Angle36)));
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect320 requires its current actor skeleton.");
            NativeBattleVector first = Observe(0xF0);
            NativeBattleVector second = Observe(0xF1);
            return new(current, retained.Write(0, BitConverter.GetBytes(unchecked((short)((first.X + second.X) / 2)))).Write(2, BitConverter.GetBytes(unchecked((short)((first.Y + second.Y) / 2)))).Write(4, BitConverter.GetBytes(unchecked((short)((first.Z + second.Z) / 2)))).Write(6, BitConverter.GetBytes(transform.Angle36)));

            NativeBattleVector Observe(byte selector)
            {
                NativeFileStaging = current.Staging;
                NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(current.Memory, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0));
                current = current with { Memory = observed, Staging = NativeFileStaging };
                return new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters.Mac[2]!.Value));
            }
        }

        NativeBattleEffect320PointStep Origin65DA30(NativeBattleEffect320Frame current, NativeBattleEffectArena input8, NativeBattleEffectArena retained)
        {
            uint modelAddress = current.State.ReadDword(NativeBattleEffect320Profile.CasterGlobal);
            uint relative = unchecked(modelAddress - 0x1D972C0u);
            if (relative % 156 != 0 || relative / 156 >= graphics.Models.Length)
            {
                throw new InvalidDataException("Effect320 bone origin requires its current physical caster row.");
            }
            int actor = checked((int)(relative / 156));
            NativeBattleGraphicsModel model = graphics.Models[actor];
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect320 requires its current caster skeleton.");
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
            short bone = BinaryPrimitives.ReadInt16LittleEndian(input8.Read(6, 2));
            if (bone < 0 || bone >= bones.Length)
            {
                throw new InvalidDataException("Effect320 bone origin leaves its current physical rig.");
            }
            NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect320 requires its current model matrix.");
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bones[bone]);
            NativeBattleGeometryRegisters geometry = current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect320 requires its current geometry registers.");
            NativeBattleScriptedColorRegisters colors = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect320 requires its current color registers.");
            geometry = NativeBattleGeometry.LoadMatrix(geometry, composed, true, true, null);
            NativeBattleVector vector = new(BinaryPrimitives.ReadInt16LittleEndian(input8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(input8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(input8.Read(4, 2)));
            NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(new(geometry, colors), 0, vector, bone));
            current = current with { Memory = current.Memory with { GeometryRegisters = transformed.Geometry, ScriptedColorRegisters = transformed.Colors } };
            for (int component = 0; component < 3; component++)
            {
                int value = transformed.Colors.Ir[component] ?? throw new NotSupportedException("Effect320 origin has no current transformed component.");
                retained = retained.Write(component * 2, BitConverter.GetBytes(unchecked((short)value)));
            }
            return new(current, retained);
        }

        NativeBattleEffect320Frame Execute(NativeBattleEffect320Frame current, NativeBattleEffect320Operation operation)
        {
            switch (operation)
            {
                case NativeBattleEffect320UploadTim505E30 upload:
                    NativeBattleEffectHeapAllocation tim = new("current-effect-texture", NativeBattleEffect320Controller.ReadBytes(current, upload.SourceAddress, resources.MainTim.Data.Length).Read(0, resources.MainTim.Data.Length).ToImmutableArray());
                    return current with { Memory = current.Memory with { Textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect320 requires its current texture queue.")).EnqueueTim505E30(tim).State } };
                case NativeBattleEffect320RegisterCamera5099A0 registration:
                    if (registration.SourceAddress != NativeBattleEffect320Profile.CameraAddress)
                    {
                        throw new NotSupportedException("Effect320 selected an unsupported camera resource.");
                    }
                    NativeBattleEffectTextureState textures = (current.Memory.Textures ?? throw new NotSupportedException("Effect320 requires its texture scheduler.")).Register5099A0(resources.Camera);
                    NativeCamera = Camera().RegisterControlResource(textures.Animation);
                    return current with { Memory = current.Memory with { Textures = textures } };
                case NativeBattleEffect320Sound501330 sound:
                    uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(sound.SourceAddress, 4));
                    Publish(current);
                    PublishNativeModuleSound(new(descriptor, sound.Flags, null) { Pan = sound.Pan });
                    return Import(current);
                case NativeBattleEffect320AllContacts506BA0 contacts:
                    Publish(current);
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    ImmutableArray<NativeBattleContactPacket> selected = Enumerable.Range(contacts.FirstContactIndex, contacts.Count).Select(nativeActionPackets.ReadContact).ToImmutableArray();
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = selected });
                    graphics = graphics with { PendingContacts = pending };
                    return Import(current);
                case NativeBattleEffect320DrawBatch draw:
                    if (draw.CursorGlobal != NativeBattleEffect320Profile.CursorGlobal)
                    {
                        throw new NotSupportedException("Effect320 selected an unsupported private packet cursor.");
                    }
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect320 requires its current ordering table.");
                    int cursor = NativeBattleWaterExecutionFields.ArenaOffset(current.State.ReadDword(draw.CursorGlobal), 0);
                    NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Effect320 requires its current COMMON arena.");
                    NativeBattleGfGeometryBatch resolved = NativeBattlePacketBatchWords.ResolveWithKnownness(draw.Batch, cursor, NativeBattleEffectArena.Length, offset =>
                    {
                        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 1);
                        return (bytes[0], known[0]);
                    });
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, resolved, draw.Flags);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, State = current.State.WriteDword(draw.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
                default:
                    throw new NotSupportedException($"Effect320 emitted unsupported service {operation.GetType().Name}.");
            }
        }
    }

    private static NativeBattleEffectArena CaptureNativeEffect320Model(NativeBattleGraphicsModel model)
    {
        NativeBattleEffectArena bytes = NativeBattleRaldo19ModelMemory.Capture(model);
        if (model.Transform is { } transform)
        {
            bytes = bytes.Write(0x26, BitConverter.GetBytes(transform.Scale38));
        }
        if (model.GeometryBounds52 is { } bounds)
        {
            bytes = bytes.Write(0x36, BitConverter.GetBytes(bounds.Minimum.Y));
        }
        return bytes;
    }

}
