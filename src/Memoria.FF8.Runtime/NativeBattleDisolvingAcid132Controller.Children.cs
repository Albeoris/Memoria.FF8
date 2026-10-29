using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleDisolvingAcid132Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDisolvingAcid132Controller
{
    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) AdvancePrimaryAct(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena context = ActContext(frame, task);
        NativeBattleGfAnimationSource source = resources.Act(task.ReadDword(0x18));
        frame = inputs.AdvanceAct(frame, address, source, 0x18, 0xB0, context).Frame;
        (frame, uint temporary) = Allocate(frame, 180);
        int age = Signed(Read(frame, address), 12);
        if (age < 9)
        {
            frame = DrawSprite(frame, temporary, SpriteBAddress, address, age, 2048, -512, inputs);
        }
        int phase = age - 4;
        if ((uint)phase < 12)
        {
            frame = DrawSprite(frame, temporary, SpriteCAddress, address, phase, 2048, -512, inputs);
        }
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 16 ? 2 : 0);
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) AdvanceImpactAct(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleDisolvingAcid132ActStep step = inputs.AdvanceAct(frame, address, resources.ImpactAct, 0x18, 0x78, ActContext(frame, task));
        return (step.Frame, step.Remaining == 0 ? 2 : 0);
    }

    public static NativeBattleDisolvingAcid132Frame DrawSprite(NativeBattleDisolvingAcid132Frame frame, uint temporary, uint source, uint address, int phase, short scale, short depth, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadCamera(frame), Vector(task, 16), Signed(task, 22), scale, depth) };
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), temporary, source, 0).Write(temporary + 4, BitConverter.GetBytes(unchecked((ushort)phase)));
        return inputs.Execute(frame with { Staging = staging }, new NativeBattleDisolvingAcid132DrawSprite180(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(132, Read(frame, address)) with { ResourceGeneration = source } });
    }

    private static NativeBattleEffectArena ActContext(NativeBattleDisolvingAcid132Frame frame, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena caller = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 16, 8), 0, NativeBattleEffectArena.CreateUnknown(12), 0, 8);
        return caller.Write(8, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, int Survivors) AdvanceSpriteScheduler(NativeBattleDisolvingAcid132Frame frame, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        frame = frame with { State = frame.State.WriteDword(TemporaryGlobal, temporary) };
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(TemplateSpriteAddress));
        foreach (int offset in new[] { 0x86, 0x88, 0x8A, 0x8E, 0x90, 0x92, 0x48, 0x50, 0x58, 0x60 })
        {
            context = context.Write(offset, new byte[2]);
        }
        foreach (int offset in new[] { 0x84, 0x8C, 0x94 })
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)1024));
        }
        context = context.Write(0x20, resources.TemplateSprite.Bytes.AsSpan().Slice(4, 4));
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleDisolvingAcid132Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[5].Advance((pool, slot) =>
        {
            current = WithPool(current, 5, pool);
            if (pool.DescriptorAddress != SpriteDescriptor || pool.Slots[slot].Callback != SpriteCallback)
            {
                throw new NotSupportedException("Disolving Acid reached an unsupported custom sprite callback.");
            }
            (current, int result) = AdvanceSprite(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[5], result);
        });
        current = WithPool(current, 5, step.Pool);
        return (Release(current, 180), step.Survivors);
    }

    public static (NativeBattleDisolvingAcid132Frame Frame, int Result) AdvanceSprite(NativeBattleDisolvingAcid132Frame frame, uint address, NativeBattleDisolvingAcid132Resources resources, NativeBattleDisolvingAcid132Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattlePackedMatrix32 camera = inputs.ReadCamera(frame);
        NativeBattleActionMatrix matrix = camera.ReadMatrix();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, false, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => camera.Known[index] ? (byte?)camera.Bytes[index] : null).ToImmutableArray() };
        NativeBattleGeometryRegisterStep registers = new(geometry, frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(task, 12), Signed(task, 18));
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        uint temporary = frame.State.ReadDword(TemporaryGlobal);
        int phase = Signed(task, 18);
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(resources.TemplateSprite.Bytes.AsSpan().Slice(10 + phase * 2, 2));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(resources.TemplateSprite.Bytes.AsSpan().Slice(offset, 4));
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0x98, BitConverter.GetBytes(unchecked(matrix.X + registers.Colors.Mac[0]!.Value))).Write(0x9C, BitConverter.GetBytes(unchecked(matrix.Y + registers.Colors.Mac[1]!.Value))).Write(0xA0, BitConverter.GetBytes(unchecked(matrix.Z + registers.Colors.Mac[2]!.Value)));
        context = context.Write(0x2C, BitConverter.GetBytes(checked(TemplateSpriteAddress + (uint)offset + 4))).Write(0x30, BitConverter.GetBytes(flags));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleDisolvingAcid132DrawTemplate180(temporary, TemplateSpriteAddress) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(132, Read(frame, address)) with { ResourceGeneration = TemplateSpriteAddress } });
        task = Read(frame, address).WriteWord(18, unchecked((ushort)(Read(frame, address).ReadWord(18) + 1)));
        if (Signed(task, 18) >= 8)
        {
            return (Write(frame, address, task), 2);
        }
        task = Vector(task, 12, new(unchecked((short)(Signed(task, 12) + Signed(task, 20))), unchecked((short)(Signed(task, 14) + Signed(task, 22))), unchecked((short)(Signed(task, 16) + Signed(task, 24)))));
        return (Write(frame, address, task), 0);
    }
}
