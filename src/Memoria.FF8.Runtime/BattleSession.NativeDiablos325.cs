using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RunNativeDiablos325(NativeBattleGraphicsState graphics, bool initialize)
    {
        NativeBattleDiablos325Frame frame = new(NativeEffectMemory ?? throw new NotSupportedException("Native Diablos requires its retained effect memory."), graphics.OriginalEffect ?? throw new InvalidDataException("Native Diablos lost its selected effect.")) { Random = nativeCrtRandom };
        if (initialize)
        {
            frame = NativeBattleDiablos325Entry.Initialize6541E0(frame, nativeActionPackets, graphics.Models);
        }
        else
        {
            NativeBattleDiablos325Inputs inputs = new(nativeActionPackets, current =>
            {
                NativeVibrationBank bank = current.Effect.Binding.Diablos325Resources?.Vibration ?? throw new NotSupportedException("Native Diablos requires its original vibration bank.");
                NativeVibrationEnqueue queued = (NativeVibrationProcess ?? throw new NotSupportedException("Native Diablos requires its retained vibration process.")).Enqueue(bank, 1, 128);
                NativeVibrationProcess = queued.State;
                return new(current, queued.Handle);
            })
            {
                QueueFile = (current, request) =>
                {
                    NativeBattleEffectFileAsset file = current.Effect.Binding.Diablos325Resources?.Files.SingleOrDefault(file => file.FileId == request.FileId) ?? throw new NotSupportedException("Native Diablos requires its requested original archive file.");
                    if (file.Data.IsDefaultOrEmpty)
                    {
                        throw new NotSupportedException("Native Diablos requested archive bytes are unavailable.");
                    }
                    NativeBattleEffectArena arena = (current.Memory.Arena ?? throw new NotSupportedException("Native Diablos file delivery requires its physical arena.")).Write(checked((int)(request.DestinationAddress - NativeBattleEffectNodeMemory.ArenaAddress)), file.Data.AsSpan());
                    NativeFileIo = (NativeFileIo ?? throw new NotSupportedException("Native Diablos file delivery requires its retained IO clock.")).Queue482610(file.Data.Length, request.CompletionCallback, null);
                    return current with { Memory = current.Memory with { Arena = arena } };
                },
                PlaySound = (sound, flags, pan) => PublishNativeModuleSound(new(sound, checked((uint)flags), null) { Pan = checked((byte)pan) }),
                PlayStream = (current, pan, volume) => PublishNativeModuleSound((current.Memory.SummonStream ?? throw new NotSupportedException("Native Diablos requires its retained summon stream number.")).Play5018C0(pan, volume)),
                PresentBody = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos body requires its current projection.");
                    NativeBattleDiablos325BodyDraw drawn = NativeBattleDiablos325BodyDrawing.Draw6574F0(current.Memory, task, projection);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos body requires its current ordering table.");
                    NativeBattlePrimitiveProcess process = drawn.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos body requires its actual COMMON packet bank.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return new(current with { Memory = drawn.Memory with { PrimitiveProcess = written.Process } }, task);
                },
                PresentWings = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos paired wings require their current projection.");
                    NativeBattleDiablos325WingsDraw drawn = NativeBattleDiablos325Wings.Advance654811(current, task, projection);
                    NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(drawn.Memory);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos paired wings require their current ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(state.PacketCursor51D0 - state.ModelBuffer5208)), primitive.Pending, drawn.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return new(current with { Memory = written.Memory with { Diablos325 = state with { PacketCursor51D0 = checked(state.ModelBuffer5208 + (uint)written.Cursor) } } }, drawn.Task);
                },
                PresentWell = (current, counter) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos well requires its current projection.");
                    NativeBattleDiablos325WellDraw drawn = NativeBattleDiablos325Well.Emit65473A(current, counter, projection);
                    NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(drawn.Memory);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos well requires its current ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(state.PacketCursor51D0 - state.ModelBuffer5208)), primitive.Pending, drawn.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return current with { Memory = written.Memory with { Diablos325 = state with { PacketCursor51D0 = checked(state.ModelBuffer5208 + (uint)written.Cursor) } } };
                },
                DrawPuffs = current =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos puffs require their current projection.");
                    NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos puffs require their current view matrix.");
                    NativeBattleDiablos325Resources resources = current.Effect.Binding.Diablos325Resources ?? throw new NotSupportedException("Native Diablos puffs require their original sprite resource.");
                    for (int index = 0; index < NativeBattleDiablos325Puffs.Capacity; index++)
                    {
                        int offset = NativeBattleDiablos325Puffs.BaseOffset + index * 16;
                        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Native Diablos puffs require their current arena.");
                        if (arena.Read(offset + 15, 1)[0] == 0)
                        {
                            continue;
                        }
                        if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4)) != 0xFD67A0)
                        {
                            throw new NotSupportedException("Native Diablos puff drawing requires its reached original sprite publication.");
                        }
                        NativeBattleActionMatrix basis = NativeBattleDiablos325PuffPackets.Basis658970(current, index, view, out byte life);
                        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325PuffPackets.Emit6FC9E0(resources.PuffSprite, life, basis, projection, 19);
                        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(current.Memory);
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos puffs require their current ordering table.");
                        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(current.Memory, checked((int)(state.PacketCursor51D0 - state.ModelBuffer5208)), primitive.Pending, batch);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                        current = current with { Memory = written.Memory with { Diablos325 = state with { PacketCursor51D0 = checked(state.ModelBuffer5208 + (uint)written.Cursor) } } };
                    }
                    return current;
                },
                PresentTail = (current, counter) =>
                {
                    NativeBattleDiablos325TailStep tail = NativeBattleDiablos325OpeningTail.Advance656203(current, NativeCamera ?? throw new NotSupportedException("Native Diablos requires its current camera owner."), counter);
                    NativeCamera = tail.Camera;
                    return tail.Frame;
                },
                PresentProgramme = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos programme requires its current projection.");
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos programme requires its original interpolation constants.");
                    NativeBattleDiablos325ProgrammeDraw draw = NativeBattleDiablos325Programme.Advance654654(current, task, projection, colors);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos programme requires its current primitive frame.");
                    NativeBattlePrimitiveProcess process = draw.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos programme requires its actual common packet bank.");
                    NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, draw.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return new(current with { Memory = draw.Memory with { PrimitiveProcess = written.Process } }, draw.Task);
                }
            };
            frame = NativeBattleDiablos325Controller.Advance654350(frame, (current, flash) =>
            {
                NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Diablos flash requires its current stage groups.");
                stage.Validate();
                uint previous = NativeBattleDiablos325Entry.RequireState(current.Memory).PreviousFlash51EC;
                NativeStage = stage with
                {
                    Groups = stage.Groups.Select(group => group with
                    {
                        ColorDepth = unchecked((short)flash),
                        ColorFlags = 0,
                        Flags = flash >= 4096 ? (byte)(group.Flags & ~2) : previous >= 4096 ? (byte)(group.Flags | 2) : group.Flags
                    }).ToImmutableArray()
                };
                return current;
            }, inputs);
        }
        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
