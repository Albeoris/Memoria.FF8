using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Step AdvanceEmbeddedTrail62C550(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        const uint model = 0x24F0EE8;
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 12) == 0)
        {
            frame = StoreModelMatrix(frame, model + 0x40, NativeBattleCuragaMatrices.Identity8DD770() with { X = 3750, Y = -5000, Z = 5400 });
            frame = InitializeModel62C790(frame, resources, model, NativeBattleEffect326Profile.EmbeddedModelAddress, 16);
            frame = SelectModel62C800(frame, resources, model, 1);
            task = task.WriteWord(0x10, 0xFE89).WriteWord(0x12, 0xFD3C).WriteWord(0x14, 0xFD1B).WriteWord(0x18, unchecked((ushort)(ParticleRoll(ref frame) & 4095))).WriteWord(0x1A, 768);
            frame = Write(frame, address, task);
            for (int index = 0; index < 6; index++)
            {
                frame = WriteBytes(frame, 0x24F0F88u + (uint)(index * 36), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0u)), 4);
            }
        }
        if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            NativeBattleActionMatrix previous = ReadPacked(frame, model + 0x40).ReadMatrix();
            frame = StoreModelMatrix(frame, model + 0x40, previous with { X = unchecked(previous.X + Signed(task, 0x10)), Y = unchecked(previous.Y + Signed(task, 0x12)), Z = unchecked(previous.Z + Signed(task, 0x14)) });
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            frame = Write(frame, address, task);
        }
        NativeBattleActionMatrix current = ReadPacked(frame, model + 0x40).ReadMatrix();
        frame = StoreModelMatrix(frame, model + 0x40, NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x18), 0, 0)) with { X = current.X, Y = current.Y, Z = current.Z });
        frame = DrawOwnedModel62C820(frame, resources, inputs, model, ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix());
        int history = Signed(task, 12) % 6;
        NativeBattleEffectArena retained = ReadBytes(frame, model + 0x40, 32);
        frame = WriteBytes(frame, checked(0x24F0F88u + (uint)(history * 36)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(1u)), 4);
        frame = WriteBytes(frame, checked(0x24F0F8Cu + (uint)(history * 36)), retained, 32);
        uint color = 0x02606060;
        for (int index = 0; index < 6; index++)
        {
            uint record = checked(0x24F0F88u + (uint)(history * 36));
            if (BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, record, 4).Read(0, 4)) == 0)
            {
                break;
            }
            frame = WriteBytes(frame, model + 0x40, ReadBytes(frame, record + 4, 32), 32);
            frame = frame with { State = frame.State.WriteDword(model + 0x28, color) };
            frame = DrawOwnedModel62C820(frame, resources, inputs, model, ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix());
            color = unchecked(color - 0x000C0C0C);
            history = history == 0 ? 5 : history - 1;
        }
        frame = WriteBytes(frame, model + 0x40, retained, 32);
        frame = frame with { State = frame.State.WriteDword(model + 0x28, 0x00808080) };
        return new(frame, Signed(task, 12) >= 30 ? 2 : 0);
    }

    public static NativeBattleEffect326Step AdvanceEmbeddedAttachment62C8F0(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        const uint model = 0x24F0EE8;
        NativeBattleCactuarRawModelState raw = RequireModel(frame, model);
        uint skeleton = checked((raw.WorkingBase ?? throw new NotSupportedException("Effect326 attachment requires its current embedded skeleton.")) + (uint)resources.EmbeddedModel.Section(0));
        NativeBattlePackedMatrix32 root = ReadPacked(frame, skeleton + 32);
        frame = WriteBytes(frame, 0x24F2288, PackedArena(root), 32);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, model + 0x40).ReadMatrix(), root.ReadMatrix());
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(world, 0x5400, 0x5400, 0x5400);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix(), scaled), null));
        if (Signed(Read(frame, address), 12) > 0)
        {
            frame = DrawMesh326(frame, resources, inputs, 0xE18B38, 0x33, null, 3);
        }
        return IncrementMeshClock(frame, address, inputs, 30);
    }

    public static NativeBattleEffect326Step AdvanceFinalOwnedModel(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Resources resources, NativeBattleEffect326Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint model = task.Callback switch
        {
            0x62CA80 => 0x24F0EE8,
            0x62CC00 => 0x24F0D50,
            _ => throw new InvalidDataException("Effect326 final model requires its actual native callback.")
        };
        int age = Signed(task, 12);
        if (age == 0)
        {
            frame = StoreModelMatrix(frame, model + 0x40, NativeBattleCuragaMatrices.Identity8DD770() with { X = -250, Y = -1800, Z = 600 });
            frame = InitializeModel62C790(frame, resources, model, ModelResource(resources, model).Address, 16);
            frame = frame with { State = frame.State.WriteWord(model, unchecked((ushort)(frame.State.ReadWord(model) | 1))) };
            frame = SelectModel62C800(frame, resources, model, 0);
        }
        else if ((inputs.ReadFlags(frame) & 0x201) == 0)
        {
            frame = AdvanceModel62CBC0(frame, resources, model);
        }
        if (age >= 19)
        {
            frame = FadeOwnedColor326(frame, inputs, model + 0x28, 4096 - NativeBattleCameraMath.Sine((age - 19) * 1024 / 8), 0x00808080);
        }
        int? drawingStack = task.Callback == 0x62CC00 ? NativeBattleEffect326Stack.ResolveLateModelDrawEntryModulo8(frame.Memory.ExecutionStack, inputs.EntryLifecycle) : null;
        frame = DrawOwnedModel62C820(frame, resources, inputs, model, ReadPacked(frame, NativeBattleEffect326Profile.ViewMatrix).ReadMatrix(), drawingStack);
        return IncrementMeshClock(frame, address, inputs, 27);
    }

    public static NativeBattleEffect326Step AdvanceFinalTint62CD40(NativeBattleEffect326Frame frame, uint address, NativeBattleEffect326Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        uint color = 0x00F08070;
        if (age >= 19)
        {
            frame = FadeOwnedColor326(frame, inputs, null, 4096 - NativeBattleCameraMath.Sine((age - 19) * 1024 / 8), color);
            color = frame.Memory.ScriptedColorRegisters?.Rgb[2] ?? throw new NotSupportedException("Effect326 final tint requires its current packed color result.");
        }
        SkinWrites packet = new(16, 0);
        uint[] words = [0x03000000, (color & 0xFFFFFF) | 0x62000000, 0, 0x06C00A00];
        for (int lane = 0; lane < words.Length; lane++)
        {
            packet.Word(lane * 4, words[lane]);
        }
        NativeBattleGeometryPrimitive primitive = new(17 + 4096, words.ToImmutableArray(), [0, 0, 0, 0]);
        frame = inputs.Execute(frame, new NativeBattleEffect326DeliverBatch(new([packet.Attempt(primitive)], 16), 0x1D8E054));
        return IncrementMeshClock(frame, address, inputs, 27);
    }

    private static NativeBattleEffect326Frame FadeOwnedColor326(NativeBattleEffect326Frame frame, NativeBattleEffect326Inputs inputs, uint? destination, int cue, uint farColor)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect326 fading requires its current primitive controls.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect326 fading requires its current far-color controls.")).PublishFarColor45DDA0((byte)farColor, (byte)(farColor >> 8), (byte)(farColor >> 16));
        NativeBattleScriptedDpcsResult faded = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect326 fading requires its current arithmetic registers."), 0, unchecked((uint)cue), far, inputs.Colors);
        frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = faded.Registers, PrimitiveProcess = process with { ModelRendererRegisters = far } } };
        return destination is uint address ? WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(faded.PackedRgb)), 4) : frame;
    }
}
