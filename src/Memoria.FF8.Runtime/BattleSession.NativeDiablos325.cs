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
                AdvanceParticles = current =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos particles require their current projection.");
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos particles require their original interpolation constants.");
                    return NativeBattleDiablos325Particles.Advance659960(current, projection, colors, (particle, packets) =>
                    {
                        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos particles require their current ordering table.");
                        NativeBattlePrimitiveProcess process = particle.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos particles require their current COMMON packet bank.");
                        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, packets);
                        graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                        return particle with { Memory = particle.Memory with { PrimitiveProcess = written.Process } };
                    });
                },
                PresentRecipients = (current, task) =>
                {
                    NativeBattleDiablos325RecipientServices services = new(actor => graphics.Models[actor], (actor, model) => graphics = graphics with { Models = graphics.Models.SetItem(actor, model) }, actor => NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Native Diablos recipient requires its current original rig."), actor =>
                    {
                        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
                        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native Diablos recipient requires its current original body geometry.");
                        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Diablos recipient requires its actual linked geometry presence.");
                        if (!linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Diablos recipient requires its original linked skeleton layout.")))
                        {
                            if (body.Length < 2 || linked.Length != 1)
                            {
                                throw new InvalidDataException("Native Diablos recipient shared weapon requires its actual geometry slot.");
                            }
                            body = body.SetItem(1, linked[0]);
                        }
                        return body;
                    }, (active, actor, scale) =>
                    {
                        var drawn = DrawNativeDiablosRecipient657CB0(active, actor, scale, graphics);
                        graphics = drawn.Graphics;
                        return drawn.Frame;
                    });
                    NativeBattleDiablos325ProgrammeStep advanced = NativeBattleDiablos325Recipients.Advance654E7E(current, task, nativeActionPackets, services);
                    NativeBattleDiablos325BodyDraw ground = NativeBattleDiablos325Ground.Draw659730(advanced.Frame, task.ReadWord(0xC), NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos sink requires its current projection."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos sink requires its original interpolation constants."));
                    return new(SubmitCommon(advanced.Frame, ground), advanced.Task);
                },
                RestoreRecipients = (current, task) =>
                {
                    int headerIndex = current.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Native Diablos actor restoration requires its retained action header.");
                    int count = nativeActionPackets.Headers[headerIndex].ReadByte(0x10);
                    for (int index = 0; index < count; index++)
                    {
                        int actor = NativeBattleDiablos325Recipients.ActorIndex(task.ReadDword(0x94 + index * 12));
                        graphics = graphics with { Models = graphics.Models.SetItem(actor, graphics.Models[actor] with { Flags0 = (ushort)(graphics.Models[actor].Flags0 & 0xFFF3) }) };
                    }
                    return current;
                },
                PresentFlash = (current, counter) => SubmitPrivate(current, new(current.Memory, NativeBattleDiablos325Flash.Emit659A10(counter)), 1),
                ApplyContact = current =>
                {
                    int headerIndex = current.Effect.Parameters?.HeaderIndex7C ?? throw new NotSupportedException("Native Diablos contact requires its retained action header.");
                    NativeEffectMemory = current.Memory;
                    var pending = graphics.PendingContacts;
                    graphics = ConsumeNativeGraphicsContacts(graphics with { PendingContacts = nativeActionPackets.ReadHeaderContacts506BA0(headerIndex) });
                    graphics = graphics with { PendingContacts = pending };
                    return current with { Memory = NativeEffectMemory ?? throw new InvalidDataException("Native Diablos contact lost its current process memory.") };
                },
                StopVibration = (current, handle) =>
                {
                    NativeVibrationProcess = (NativeVibrationProcess ?? throw new NotSupportedException("Native Diablos completion requires its retained vibration process.")).Stop4A2940(handle);
                    return current;
                },
                PresentLateProgramme = (current, task, second) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos late programme requires its current projection.");
                    NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos late programme requires its original color interpolation constants.");
                    NativeBattleDiablos325ProgrammeDraw draw = second ? NativeBattleDiablos325LateProgramme.Advance654CE5(current, task, projection, colors) : NativeBattleDiablos325LateProgramme.Advance654BF8(current, task, projection, colors);
                    return new(SubmitCommon(current, new(draw.Memory, draw.Packets)), draw.Task);
                },
                PresentSecondaryBody = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos secondary body requires its current projection.");
                    bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Diablos secondary body requires its current hardware capability.");
                    NativeBattleDiablos325BodyDraw draw = NativeBattleDiablos325RawBodyDrawing.Draw6A79B0(current.Memory, task, projection, hardware, 0x1A8, 0x254);
                    return new(SubmitCommon(current, draw), task);
                },
                DrawTrails = current => SubmitPrivate(current, NativeBattleDiablos325Trail.Draw657F00(current, NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos trails require their current projection."))),
                PresentLateStatic = (current, counter) => SubmitPrivate(current, NativeBattleDiablos325LateStatic.Draw654DDC(current, counter, NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos late model requires its current projection."), definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos late model requires its original color interpolation constants."))),
                UploadLateTexture = (current, source) =>
                {
                    NativeBattleEffectTextureState textures = current.Memory.Textures ?? throw new NotSupportedException("Native Diablos late texture requires its current upload queue.");
                    textures = textures.EnqueueArenaTim505E30(current.Memory.Arena ?? throw new NotSupportedException("Native Diablos late texture requires its actual source arena."), NativeBattleWaterExecutionFields.ArenaOffset(source, 1)).State;
                    return current with { Memory = current.Memory with { Textures = textures } };
                },
                PresentShapes = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos shapes require their current projection.");
                    NativeBattleDiablos325ShapeDraw drawn = NativeBattleDiablos325Shapes.Advance654899(current, task, projection);
                    NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(drawn.Memory);
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos shapes require their current ordering table.");
                    NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(drawn.Memory, checked((int)(state.PacketCursor51D0 - state.ModelBuffer5208)), primitive.Pending, drawn.Packets);
                    graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                    return new(current with { Memory = written.Memory with { Diablos325 = state with { PacketCursor51D0 = checked(state.ModelBuffer5208 + (uint)written.Cursor) } } }, drawn.Task);
                },
                PresentBody = (current, task) =>
                {
                    NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos body requires its current projection.");
                    NativeBattleDiablos325BodyDraw drawn;
                    if (task.ReadWord(0xC) is >= 268 and <= 327)
                    {
                        drawn = NativeBattleDiablos325BodyDrawing.Draw6574F0(current.Memory, task, projection, -8892, 0, 128);
                    }
                    else if (task.ReadWord(0xC) >= 167)
                    {
                        bool hardware = definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Diablos ordinary body requires its current hardware capability.");
                        drawn = NativeBattleDiablos325RawBodyDrawing.Draw6A79B0(current.Memory, task, projection, hardware);
                    }
                    else
                    {
                        drawn = NativeBattleDiablos325BodyDrawing.Draw6574F0(current.Memory, task, projection);
                    }
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
                        uint address = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
                        ImmutableArray<byte> sprite = resources.ReadPuffSprite(address);
                        NativeBattleActionMatrix basis = NativeBattleDiablos325PuffPackets.Basis658970(current, index, view, out byte life);
                        NativeBattleGfGeometryBatch batch = NativeBattleDiablos325PuffPackets.Emit6FC9E0(sprite, life, basis, projection, 19, NativeBattlePilotVisualLifetimes.Identity(325, arena, checked(NativeBattleDiablos325Entry.RequireState(current.Memory).ModelBuffer5208 + NativeBattleDiablos325Puffs.BaseOffset), index));
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
        NativeBattleDiablos325Frame SubmitCommon(NativeBattleDiablos325Frame current, NativeBattleDiablos325BodyDraw draw)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos drawing requires its current ordering table.");
            NativeBattlePrimitiveProcess process = draw.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos drawing requires its current COMMON packet owner.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, draw.Packets);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = draw.Memory with { PrimitiveProcess = written.Process } };
        }

        NativeBattleDiablos325Frame SubmitPrivate(NativeBattleDiablos325Frame current, NativeBattleDiablos325BodyDraw draw, ushort flags = 0)
        {
            NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(draw.Memory);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos drawing requires its current ordering table.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(draw.Memory, checked((int)(state.PacketCursor51D0 - state.ModelBuffer5208)), primitive.Pending, draw.Packets, flags);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = written.Memory with { Diablos325 = state with { PacketCursor51D0 = checked(state.ModelBuffer5208 + (uint)written.Cursor) } } };
        }

        NativeEffectMemory = frame.Memory;
        nativeCrtRandom = frame.Random;
        return graphics with { OriginalEffect = frame.Effect };
    }
}
