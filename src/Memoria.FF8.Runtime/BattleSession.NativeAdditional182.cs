using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeAdditional182(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new NotSupportedException("Additional effect182 requires its selected original effect.");
        NativeBattleAdditional182Resources resources = effect.Binding.Additional182Resources ?? throw new NotSupportedException("Additional effect182 requires its original resources.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Additional effect182 requires its actual process memory.");
        NativeBattleAdditional182Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Additional effect182 requires its actual CRT stream."), memory.Additional182 ?? throw new NotSupportedException("Additional effect182 requires its retained executable storage.")) { Staging = NativeFileStaging ?? throw new NotSupportedException("Additional effect182 requires its physical temporary memory.") };
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Additional effect182 requires its interpolation constants.");
        NativeBattleAdditional182Inputs inputs = new(nativeActionPackets, colors, graphics.Flags, _ => Camera().CapturePackedView1D97778(), _ => Camera().Projection ?? throw new NotSupportedException("Additional effect182 requires its current projection."), (_, actor) => graphics.Models[actor], Anchor, Execute) { CapturePresentation = true };
        frame = initialize ? NativeBattleAdditional182Controller.Initialize(frame, resources, inputs) : NativeBattleAdditional182Controller.Advance(frame, resources, inputs);
        Publish(frame);
        if (initialize)
        {
            NativeCamera = Camera().RegisterControlResource(frame.Memory.Textures!.Animation);
        }
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraState Camera() => NativeCamera ?? throw new NotSupportedException("Additional effect182 requires its actual camera.");

        void Publish(NativeBattleAdditional182Frame current)
        {
            NativeEffectMemory = current.Memory with { Additional182 = current.State };
            NativeFileStaging = current.Staging ?? throw new NotSupportedException("Additional effect182 lost its actual temporary memory.");
            nativeCrtRandom = current.Random;
        }

        NativeBattleAdditional182Frame Import(NativeBattleAdditional182Frame current)
        {
            NativeBattleEffectMemory latest = NativeEffectMemory ?? throw new InvalidDataException("Additional effect182 external service lost its process memory.");
            return current with { Memory = latest, State = latest.Additional182 ?? throw new InvalidDataException("Additional effect182 external service lost its executable storage."), Staging = NativeFileStaging ?? throw new InvalidDataException("Additional effect182 external service lost its temporary memory."), Random = nativeCrtRandom ?? throw new InvalidDataException("Additional effect182 external service lost its CRT stream.") };
        }

        NativeBattleAdditional182PointStep Anchor(NativeBattleAdditional182Frame current, int actor)
        {
            return new(current, Point(ReadNativeDefaultPosition571400(graphics, actor)));
        }

        static NativeBattleEffectArena Point(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));

        NativeBattleAdditional182Frame Deliver(NativeBattleAdditional182Frame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Additional effect182 requires its pending ordering table.");
            uint cursor = current.State.ReadDword(NativeBattleAdditional182Profile.CursorGlobal);
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(cursor - NativeBattleEffectNodeMemory.ArenaAddress)), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, State = current.State.WriteDword(NativeBattleAdditional182Profile.CursorGlobal, checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)written.Cursor)) };
        }

        NativeBattleAdditional182Frame Execute(NativeBattleAdditional182Frame current, NativeBattleAdditional182Operation operation)
        {
            switch (operation)
            {
                case NativeBattleAdditional182Sound5013A0 sound:
                    Publish(current);
                    PublishNativeModuleSound(sound.Packed, sound.Flags, new(BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(sound.Point8.Read(4, 2)), 0));
                    return Import(current);
                case NativeBattleAdditional182ContactsAll contacts:
                    Publish(current);
                    graphics = ApplyNativeHeaderContacts(graphics, contacts.HeaderIndex);
                    return Import(current);
                case NativeBattleAdditional182ActorFlags flags:
                    graphics = graphics with { Models = graphics.Models.SetItem(flags.Actor, graphics.Models[flags.Actor] with { Flags0 = unchecked((ushort)((graphics.Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask)) }) };
                    return current;
                case NativeBattleAdditional182ActorAlpha alpha:
                    graphics = graphics with { Models = graphics.Models.SetItem(alpha.Actor, graphics.Models[alpha.Actor] with { Alpha7 = alpha.Alpha }) };
                    return current;
                case NativeBattleAdditional182ActorRecolor recolor:
                    NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Additional effect182 requires its current color control owner.");
                    NativeBattleModelRendererRegisters far = process.ModelRendererRegisters ?? throw new NotSupportedException("Additional effect182 requires its current far controls.");
                    NativeBattleScriptedColorRegisters colorRegisters = current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Additional effect182 requires its current GTE data registers.");
                    far = LoadFar(colors.Normal, far);
                    NativeBattleScriptedDpcsResult normal = NativeBattleScriptedDpcs.Transform45F270(colorRegisters, 0x32000000, unchecked((uint)recolor.Cue), far, colors);
                    far = LoadFar(colors.Transition, far);
                    NativeBattleScriptedDpcsResult shadow = NativeBattleScriptedDpcs.Transform45F270(normal.Registers, 0, normal.Registers.Ir0!.Value, far, colors);
                    graphics = graphics with { Models = graphics.Models.SetItem(recolor.Actor, graphics.Models[recolor.Actor] with { Color44 = normal.PackedRgb, Color40 = (shadow.PackedRgb & 0xFFFFFF) | 0x02000000 }) };
                    return current with { Memory = current.Memory with { ScriptedColorRegisters = shadow.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
                case NativeBattleAdditional182Stage5713E0 stage:
                    NativeBattleStageState retained = NativeStage ?? throw new NotSupportedException("Additional effect182 requires its retained stage groups.");
                    NativeStage = retained with { Groups = retained.Groups.Select(group => group with { ColorDepth = unchecked((short)stage.Value), ColorFlags = stage.Flags }).ToImmutableArray() };
                    return current;
                case NativeBattleAdditional182DrawSprite180 sprite:
                    return DrawSprite(current, sprite.TemporaryAddress, sprite.SourceAddress, sprite.TableBaseBucket, unchecked((byte)(sprite.DepthShift & 31)), pilotIdentity: sprite.PilotIdentity);
                case NativeBattleAdditional182DrawStatic88 mesh:
                    return DrawMesh(current, mesh.TemporaryAddress, mesh.SourceAddress, mesh.TableBaseBucket, unchecked((byte)(mesh.DepthShift & 31)), pilotIdentity: mesh.PilotIdentity);
                default:
                    throw new NotSupportedException("Additional effect182 emitted an unsupported native operation.");
            }
        }

        NativeBattleAdditional182Frame DrawSprite(NativeBattleAdditional182Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleModuleResource resource = resources.ReadSprite(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Additional effect182 sprite requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 180);
            if (BinaryPrimitives.ReadUInt32LittleEndian(temporary.Read(0, 4)) != sourceAddress)
            {
                throw new InvalidDataException("Additional effect182 sprite arguments differ from its physical temporary.");
            }
            NativeBattleModuleSpriteDraw drawn = NativeBattleModuleSpriteDrawing.Draw571C80(current.Memory, temporary, selected => selected == resource.Address ? resource : throw new InvalidDataException("Additional effect182 sprite dispatcher changed its original source."), Camera().Projection ?? throw new NotSupportedException("Additional effect182 sprite requires its projection."), Camera().Roll, bucket, shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Temporary180, 180) };
            return Deliver(current, pilotIdentity.HasValue ? NativeBattleAdditional182Visuals.PreserveNativeDepths(drawn.Batch) : drawn.Batch);
        }

        NativeBattleModelRendererRegisters LoadFar(uint rgb, NativeBattleModelRendererRegisters far) => far with { FarRed = (rgb & 255) << 4, FarGreen = ((rgb >> 8) & 255) << 4, FarBlue = ((rgb >> 16) & 255) << 4 };

        NativeBattleAdditional182Frame DrawMesh(NativeBattleAdditional182Frame current, uint address, uint sourceAddress, int bucket, byte shift, NativeBattlePilotVisualIdentity? pilotIdentity = null)
        {
            NativeBattleStaticMeshResource mesh = resources.ReadMesh(sourceAddress);
            NativeBattleFileStaging staging = current.Staging ?? throw new NotSupportedException("Additional effect182 mesh requires its actual temporary memory.");
            NativeBattleEffectArena temporary = Temporary(staging, address, 88);
            NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(current.Memory, mesh, Camera().Projection ?? throw new NotSupportedException("Additional effect182 requires its current projection."), temporary, colors, bucket, shift, pilotIdentity: pilotIdentity);
            current = current with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(staging, address, drawn.Context, 88) };
            return Deliver(current, pilotIdentity.HasValue ? NativeBattleAdditional182Visuals.PreserveNativeDepths(drawn.Batch) : drawn.Batch);
        }

        static NativeBattleEffectArena Temporary(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
