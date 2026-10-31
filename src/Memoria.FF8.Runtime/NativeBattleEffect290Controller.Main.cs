using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect290Controller
{
    public static NativeBattleEffect290Step AdvanceMain6767B0(NativeBattleEffect290Frame frame, uint address, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        short age = Signed(Read(frame, address), 12);
        if (age >= 90)
        {
            return new(frame, 2);
        }
        uint caster = frame.State.ReadDword(CasterGlobal);
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        if (age < 16)
        {
            NativeBattleEffect290PointStep query = inputs.Point502170(frame, caster, 0, 0, point);
            frame = query.Frame;
            point = query.Output8;
            NativeBattleVector position = Vector(point);
            NativeBattleEffectArena caller = ProgrammeCaller290(frame, position.X, position.Y - 1000, position.Z, 0, -512, inputs);
            NativeBattleEffect290Step programme = (inputs.AdvanceProgramme ?? throw new NotSupportedException("Effect290 requires its selected programme provider."))(frame, address + 0x64E0, 0x110, caller, resources, inputs);
            frame = programme.Frame;
            frame = DrawMainSprite290(frame, address, point, age, resources, inputs);
        }
        if (age is >= 20 and < 42)
        {
            NativeBattleEffect290PointStep query = inputs.Point502170(frame, caster, 1, 1024, point);
            frame = query.Frame;
            point = query.Output8;
            NativeBattleVector position = Vector(point);
            NativeBattleEffectArena caller = ProgrammeCaller290(frame, position.X, 0, position.Z, 2048, 0, inputs);
            NativeBattleEffect290Step programme = (inputs.AdvanceProgramme ?? throw new NotSupportedException("Effect290 requires its selected programme provider."))(frame, address + 0x65F0, 0x22C, caller, resources, inputs);
            frame = programme.Frame;
            programme = (inputs.AdvanceProgramme ?? throw new NotSupportedException("Effect290 requires its selected programme provider."))(frame, address + 0x681C, 0xC8, caller, resources, inputs);
            frame = programme.Frame;
        }
        if (age < 20)
        {
            frame = BlendModelColor290(frame, address, age, inputs);
        }
        if (age >= 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290DrawBody676B40(caster, address + 24) { PilotIdentity = inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(290, frame.Effect.Pools, address, caster, address + 24) : null });
            frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(caster, 1, 255, 4));
        }
        if (age >= 30)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290ModelFlags(caster, 1, 255, 8));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect290DrawFragments676EF0(address));
        frame = inputs.Execute(frame, new NativeBattleEffect290DrawSmoke677690(address));
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290Sound501330(SoundAddress, 0, 128));
        }
        else if (age == 86)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect290VibrationStop4A2940(unchecked((int)Read(frame, address).ReadDword(16))));
        }
        if (age < 25)
        {
            frame = BirthSmallChildren290(frame, caster, point, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect290AdvanceFragments6774C0(address));
        return new(Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleEffectArena ProgrammeCaller290(NativeBattleEffect290Frame frame, int x, int y, int z, int angle, int depth, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(0, unchecked((short)angle), 0)) with { X = x, Y = y, Z = z };
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), 0);
        return CopyBytes(PackedArena(matrix), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 56), 0, 32).Write(0x2C, BitConverter.GetBytes(0u)).Write(0x30, BitConverter.GetBytes(depth)).Write(0x34, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + MorphOffset)));
    }

    private static NativeBattleEffect290Frame DrawMainSprite290(NativeBattleEffect290Frame frame, uint main, NativeBattleEffectArena point, short age, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Vector(point), null, 16384, -1024) };
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } } };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(MainSpriteAddress)).Write(4, BitConverter.GetBytes(age)).Write(0x24, BitConverter.GetBytes((short)0));
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleModuleSpriteDraw draw = NativeBattleModuleSpriteDrawing.Draw571C80(frame.Memory, context, resources.Sprite, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), 17, 2, pilotIdentity: inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(290, frame.Effect.Pools, main, MainSpriteAddress) : null);
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect290DrawBatch(inputs.CapturePresentation ? CaptureSpriteDepths290(draw.Batch) : draw.Batch, CursorGlobal));
        return Release(frame, 180);
    }

    internal static NativeBattleGfGeometryBatch CaptureSpriteDepths290(NativeBattleGfGeometryBatch batch)
    {
        return batch with
        {
            Attempts = batch.Attempts.Select(attempt =>
            {
                if (attempt.Primitive is not { PilotGeometry: { } visual } primitive)
                {
                    return attempt;
                }
                if (primitive.VertexPayload4.Length != 4)
                {
                    throw new InvalidDataException("Effect290 accepted sprite capture requires its actual four native depth lanes.");
                }
                return attempt with { Primitive = primitive with { PilotGeometry = visual with { SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), UseSourceDepths = true } } };
            }).ToImmutableArray()
        };
    }

    private static NativeBattleEffect290Frame BlendModelColor290(NativeBattleEffect290Frame frame, uint main, short age, NativeBattleEffect290Inputs inputs)
    {
        byte[] first = TaskView(Read(frame, main), 20, 3).Read(0, 3);
        byte[] second = frame.State.Read(BlendColorAddress, 3);
        uint firstRgb = first[0] | (uint)first[1] << 8 | (uint)first[2] << 16;
        uint secondRgb = second[0] | (uint)second[1] << 8 | (uint)second[2] << 16;
        int factor = age * 4096 / 20;
        NativeBattleWeightedColorStep blend = NativeBattleWeightedColor56CC00.Blend(frame.Memory, firstRgb, secondRgb, unchecked((short)(4096 - factor)), unchecked((short)factor), 0, inputs.Colors);
        frame = frame with { Memory = blend.Memory };
        byte[] output = [(byte)blend.Color, (byte)(blend.Color >> 8), (byte)(blend.Color >> 16)];
        uint caster = frame.State.ReadDword(CasterGlobal);
        frame = inputs.Execute(frame, new NativeBattleEffect290WriteModelBytes(caster, 44, NativeBattleEffectArena.Clear().Write(0, output), 3));
        return inputs.Execute(frame, new NativeBattleEffect290WriteModelBytes(caster, 47, NativeBattleEffectArena.Clear().Write(0, [0x32]), 1));
    }

    public static NativeBattleEffect290Frame BirthSmallChildren290(NativeBattleEffect290Frame frame, uint model, NativeBattleEffectArena retainedPoint, NativeBattleEffect290Inputs inputs)
    {
        (frame, uint particle) = Append(frame, ParticleDescriptor, 0x6775F0);
        NativeBattleEffect290PointStep query = inputs.Point502170(frame, model, 0, 0, retainedPoint);
        frame = query.Frame;
        NativeBattleEffectArena point = query.Output8;
        if (particle != 0)
        {
            NativeBattleModulePoolTask task = CopyTask(Read(frame, particle), 12, point, 8).WriteWord(18, 0);
            task = task.WriteByte(20, unchecked((byte)((Roll290(ref frame) & 15) - 16))).WriteByte(21, unchecked((byte)(0xE0 - (Roll290(ref frame) & 15)))).WriteByte(22, unchecked((byte)(0xF0 - (Roll290(ref frame) & 15))));
            frame = Write(frame, particle, task);
        }
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            (frame, uint sprite) = Append(frame, SpriteDescriptor, 0x677550);
            if (sprite == 0)
            {
                continue;
            }
            NativeBattleModulePoolTask task = Read(frame, sprite);
            for (int axis = 0; axis < 3; axis++)
            {
                int coordinate = Word290(point, axis * 2) + (Roll290(ref frame) & 511) - 256;
                task = task.WriteWord(12 + axis * 2, unchecked((ushort)coordinate));
            }
            task = task.WriteWord(18, 0).WriteByte(20, unchecked((byte)((Roll290(ref frame) & 15) - 16))).WriteByte(21, unchecked((byte)(0xC1 - (Roll290(ref frame) & 63)))).WriteByte(22, unchecked((byte)(0xF0 - (Roll290(ref frame) & 15))));
            frame = Write(frame, sprite, task);
        }
        return frame;
    }

    private static int Roll290(ref NativeBattleEffect290Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
