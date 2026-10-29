using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeThundaga105(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect ?? throw new InvalidDataException("Native Thundaga requires its selected module.");
        NativeBattleThundaga105Resources resources = effect.Binding.Thundaga105Resources ?? throw new NotSupportedException("Native Thundaga requires its original module resources.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native Thundaga requires its authenticated interpolation constants.");
        NativeBattleThundaga105Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Thundaga requires its retained process memory."), effect, nativeCrtRandom ?? throw new NotSupportedException("Native Thundaga requires its retained CRT state."));
        NativeBattleActionMatrix? billboard = initialize ? null : NativeBattleGfMatrices.Normalize67AAE0(View());
        NativeBattleThundaga105Inputs inputs = new(nativeActionPackets, Bone, actor => ReadNativeModelAnchor(graphics, actor), actor => graphics.Models[actor].Color40 ?? throw new NotSupportedException("Native Thundaga requires its actor color DWORD."), actor => Transform(actor).Scale38, DrawPrimary, Execute);
        frame = initialize ? NativeBattleThundaga105Controller.Initialize6D3DE0(frame, inputs, resources) : NativeBattleThundaga105Controller.Advance6D3E50(frame, inputs, resources);
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };

        NativeBattleCameraProjection Projection() => NativeCamera?.Projection ?? throw new NotSupportedException("Native Thundaga requires its published projection.");
        NativeBattleActionMatrix View() => Projection().View ?? throw new NotSupportedException("Native Thundaga requires its published camera view.");
        NativeBattleModelTransform Transform(int actor) => graphics.Models[actor].Transform ?? throw new NotSupportedException("Native Thundaga requires its current actor transform.");

        ImmutableArray<byte> Bone(int actor)
        {
            NativeBattleVector xyz = ReadNativeGraphicsBone(graphics, actor, 241, 0);
            byte[] raw = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(raw, xyz.X);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2), xyz.Y);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4), xyz.Z);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(6), Transform(actor).Angle36);
            return raw.ToImmutableArray();
        }

        NativeBattleThundaga105TaskStep DrawPrimary(NativeBattleThundaga105Frame current, NativeBattleModulePoolTask task, int actor, ImmutableArray<byte> anchor)
        {
            NativeBattleThundaga105ProgramDraw drawn = NativeBattleThundaga105ProgramDrawing.Advance6D43B0(current.Memory, task, resources.ProgramSource, resources.NodeSelector.Data, anchor, View(), billboard ?? throw new NotSupportedException("Native Thundaga requires its current root billboard matrix."), Projection(), colors);
            current = Append(current with { Memory = drawn.Memory }, drawn.Packets);
            return new(current, drawn.Task);
        }

        NativeBattleThundaga105Frame Append(NativeBattleThundaga105Frame current, NativeBattleGfGeometryBatch packets)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Thundaga requires its initialized ordering table.");
            int cursor = current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Thundaga requires its actual alternating private draw cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, cursor, primitive.Pending, packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }

        NativeBattleThundaga105Frame Execute(NativeBattleThundaga105Frame current, NativeBattleThundaga105Operation operation)
        {
            NativeEffectMemory = current.Memory;
            switch (operation)
            {
                case NativeBattleThundaga105InitializeTextures:
                    RegisterNativeOriginalEffectAnimation(current.Effect, resources.Animation, 0x2543D38);
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleThundaga105DebrisDraw:
                    return Append(current, NativeBattleThundaga105Debris.Draw6D41E0(current.Memory, View(), Projection()));
                case NativeBattleThundaga105FragmentDraw fragment:
                    return Append(current, NativeBattleThundaga105Fragments.Draw6D5AD0(fragment.Task, fragment.Mesh, View(), Projection(), colors));
                case NativeBattleThundaga105Sprite sprite:
                    return DrawSprite(current, sprite.Task);
                case NativeBattleThundaga105Capture capture:
                    StageFlash(0);
                    NativeBattleThundaga105CaptureTransform transform = NativeBattleThundaga105CaptureGeometry.Transform6D5620(capture.Anchor, capture.Bounds, Projection());
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Thundaga capture requires its initialized ordering table.");
                    NativeBattleThundaga105CaptureWrite written = NativeBattleThundaga105CapturePackets.Append6D5620(current.Memory, NativeStage ?? throw new NotSupportedException("Native Thundaga capture requires its current stage."), primitive.Pending, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Thundaga capture requires its private draw cursor."), RequireNativeStageGeometry(), transform, colors);
                    NativeStage = written.Stage;
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
                case NativeBattleThundaga105ActorColor color:
                    graphics = graphics with { Models = graphics.Models.SetItem(color.Actor, graphics.Models[color.Actor] with { Color40 = color.Color40 }) };
                    return current;
                case NativeBattleThundaga105StageFlash flash:
                    StageFlash(flash.Amount);
                    return current;
                case NativeBattleThundaga105CommonFlash flash:
                    graphics = graphics with { ModuleEffects = graphics.ModuleEffects.AppendFlash(unchecked((short)flash.First), unchecked((short)flash.Second), unchecked((short)flash.Third), unchecked((short)flash.Fourth)) };
                    return current;
                case NativeBattleThundaga105Sound sound:
                    PublishNativeModuleSound(new(resources.Sound, 0x101, sound.Position) { Pan = Projection().ReadPan(new(sound.Position.X, sound.Position.Y, sound.Position.Z)) });
                    return current;
                case NativeBattleThundaga105Contact contact:
                    ImmutableArray<NativeBattleContactPacket> pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [nativeActionPackets.ReadContact(contact.Contact)] });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory! };
                case NativeBattleThundaga105LoadCommon load:
                    (NativeBattleAuxiliaryState auxiliary, _) = graphics.Auxiliary.AppendCommonTextureLoad508630(load.DestinationAddress, load.CompletionWordAddress, (address, value) => current = current with { Effect = NativeBattleThundaga105Controller.WriteCompletionWord(current.Effect, address, value) });
                    graphics = graphics with { Auxiliary = auxiliary };
                    return current;
                default:
                    throw new NotSupportedException("Native Thundaga operation requires its original callback contract.");
            }
        }

        void StageFlash(int amount)
        {
            NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Thundaga requires its retained stage context rows.");
            NativeStage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = unchecked((short)amount), ColorFlags = 0 }).ToImmutableArray() };
        }

        NativeBattleThundaga105Frame DrawSprite(NativeBattleThundaga105Frame current, NativeBattleModulePoolTask task)
        {
            NativeBattleModuleResource resource = task.ReadDword(0x10) switch
            {
                0x13115DC => resources.SpriteMoving,
                0x131136C => resources.SpriteStationary,
                _ => throw new NotSupportedException("Native Thundaga sprite requires its actual source pointer.")
            };
            short scale = unchecked((short)task.ReadWord(0x1A));
            int counter = unchecked((short)task.ReadWord(0xC));
            NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
            {
                PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(105, task),
                Resource = resource,
                SpriteFrame = resource.SpriteFrame(counter),
                Phase = unchecked((ushort)counter),
                Anchor = new(unchecked((short)task.ReadWord(0x14)), unchecked((short)task.ReadWord(0x16)), unchecked((short)task.ReadWord(0x18)), 0),
                Scale = scale,
                DepthOffset = -(scale >> 2),
                Direction = 4096,
                SpriteFlags = 0,
                SpriteColor = 0x808080
            };
            NativeBattleModulePacketBatch batch = NativeBattleModulePackets.EmitSprite571DC0(draw, Projection(), NativeCamera!.Roll, 17, 2);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Thundaga sprites require their initialized ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, current.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Thundaga sprites require their actual private cursor."), primitive.Pending, batch, NativeBattleEffectGouraudWrites.FullDword);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory, Effect = current.Effect with { DrawArenaOffset = written.Cursor } };
        }
    }

    private bool TryWriteNativeThundaga105Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - (NativeBattleEffectNodeMemory.ArenaAddress + 0x1CD4));
        if (delta >= 32 * 0x2C || delta % 0x2C != 0xE)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x6D3DB0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleThundaga105Controller.WriteCompletionWord(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState current = graphics.OriginalEffect is { Binding.Entry: 0x6D3DB0 } selected ? selected : throw new NotSupportedException("Native Thundaga completion lost its physical task owner.");
            updated = graphics with { OriginalEffect = NativeBattleThundaga105Controller.WriteCompletionWord(current, address, value) };
        }
        return true;
    }
}
