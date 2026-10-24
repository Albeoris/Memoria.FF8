using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect174Lower
{
    public static NativeBattleEffect174Frame SetStageFlags5520(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs) => SetStageFlags174(frame, true, inputs);
    public static NativeBattleEffect174Frame SetStageFlags5540(NativeBattleEffect174Frame frame, NativeBattleEffect174Inputs inputs) => SetStageFlags174(frame, false, inputs);

    private static NativeBattleEffect174Frame SetStageFlags174(NativeBattleEffect174Frame frame, bool set, NativeBattleEffect174Inputs inputs)
    {
        for (int group = 0; group < 4; group++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect174StageGroupFlags(group, set ? byte.MaxValue : (byte)0xFD, set ? (byte)2 : (byte)0));
        }
        return frame;
    }

    public static NativeBattleEffect174Frame InitializeActorFade6D30(NativeBattleEffect174Frame frame, int duration, NativeBattleEffect174Inputs inputs) => InitializeActorFade174(frame, duration, 0x5D6DA0, inputs);
    public static NativeBattleEffect174Frame InitializeActorFade6EE0(NativeBattleEffect174Frame frame, int duration, NativeBattleEffect174Inputs inputs) => InitializeActorFade174(frame, duration, 0x5D6F50, inputs);

    private static NativeBattleEffect174Frame InitializeActorFade174(NativeBattleEffect174Frame frame, int duration, uint callback, NativeBattleEffect174Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        int target = checked((int)frame.State.ReadDword(TargetGlobal));
        for (int actor = 0; actor < 7; actor++)
        {
            ushort flags = inputs.ReadModel(frame, actor).Flags0;
            if ((flags & 2) == 0 || actor == caster || actor == target)
            {
                continue;
            }
            (frame, uint address) = Append(frame, ChildDescriptor, callback);
            if (address == 0)
            {
                throw new NotSupportedException("Effect174 actor fade exhausted its current physical task bank.");
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, (ushort)actor).WriteWord(24, unchecked((ushort)duration)).WriteWord(26, (ushort)(flags & 0x800)));
        }
        return frame;
    }

    public static NativeBattleEffect174Step AdvanceActorServices(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Resources resources, NativeBattleEffect174Inputs inputs) => Read(frame, address).Callback switch
    {
        0x5D6DA0 => AdvanceActorFade174(frame, address, false, inputs),
        0x5D6F50 => AdvanceActorFade174(frame, address, true, inputs),
        0x5D7220 => AdvanceActorMotion7220(frame, address, inputs),
        _ => throw new NotSupportedException("Effect174 actor service requires its selected callback.")
    };

    private static NativeBattleEffect174Step AdvanceActorFade174(NativeBattleEffect174Frame frame, uint address, bool hide, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        int age = Signed(task, 12);
        int duration = Signed(task, 24);
        int factor = unchecked(age << 12) / duration;
        if (hide)
        {
            factor = unchecked(4096 - factor);
            frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 7, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0 }), 1));
        }
        (frame, uint color44) = ActorColor174(frame, 0x32000000, inputs.ReadColor(frame, 0xB8B9A8), unchecked((uint)factor), inputs);
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 44, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(color44)), 4));
        uint retainedFactor = frame.Memory.ScriptedColorRegisters?.Ir0 ?? throw new NotSupportedException("Effect174 actor fade requires its current first interpolation factor.");
        (frame, uint color40) = ActorColor174(frame, 0, inputs.ReadColor(frame, 0xB8B7D8), retainedFactor, inputs);
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 40, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(color40)), 4));
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 43, NativeBattleEffectArena.Clear().Write(0, new byte[] { 2 }), 1));
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        if (age == 0)
        {
            ushort flags = inputs.ReadModel(frame, actor).Flags0;
            flags = unchecked((ushort)((hide ? flags : flags & 0xFFFB) | 0x800));
            frame = WriteFlags174(frame, actor, flags, inputs);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next < duration)
        {
            return new(frame, 0);
        }
        if (!hide)
        {
            uint restored = inputs.ReadColor(frame, 0xB8B7D8);
            frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 40, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(restored)), 4));
        }
        ushort current = inputs.ReadModel(frame, actor).Flags0;
        ushort final = unchecked((ushort)((current & 0xF7FF) | Read(frame, address).ReadWord(26) | (hide ? 4 : 0)));
        frame = WriteFlags174(frame, actor, final, inputs);
        return new(frame, 2);
    }

    private static (NativeBattleEffect174Frame Frame, uint Color) ActorColor174(NativeBattleEffect174Frame frame, uint source, uint farColor, uint factor, NativeBattleEffect174Inputs inputs)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect174 actor fade requires its current primitive process.");
        NativeBattleModelRendererRegisters far = (process.ModelRendererRegisters ?? throw new NotSupportedException("Effect174 actor fade requires current far controls.")).PublishModelColor(farColor);
        NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect174 actor fade requires its current arithmetic registers."), source, factor, far, inputs.Colors);
        return (frame with { Memory = frame.Memory with { PrimitiveProcess = process with { ModelRendererRegisters = far }, ScriptedColorRegisters = result.Registers } }, result.PackedRgb);
    }

    public static NativeBattleEffect174Step AdvanceActorMotion7220(NativeBattleEffect174Frame frame, uint address, NativeBattleEffect174Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int actor = Signed(task, 14);
        int age = Signed(task, 12);
        uint savedAddress = checked(SavedActorsAddress + (uint)(actor * 20));
        if (age == 0)
        {
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, actor);
            frame = WriteBytes(frame, savedAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)(inputs.ReadModel(frame, actor).Flags0 & 0x1020))), 2);
            frame = WriteBytes(frame, savedAddress + 12, CopyBytes(model, 28, NativeBattleEffectArena.Clear(), 0, 8), 8);
            frame = WriteBytes(frame, savedAddress + 4, CopyBytes(model, 12, NativeBattleEffectArena.Clear(), 0, 8), 8);
        }
        frame = WriteFlags174(frame, actor, unchecked((ushort)(inputs.ReadModel(frame, actor).Flags0 | 0x1020)), inputs);
        short angleX = unchecked((short)(unchecked(Signed(task, 24) * age) / 30));
        short angleY = unchecked((short)(unchecked(age << 12) / 30));
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 12, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(angleX)), 2));
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(angleY)), 2));
        NativeBattleVector anchor = Vector(TaskView(task, 16, 8));
        NativeBattleVector original = Vector(ReadBytes(frame, savedAddress + 12, 8));
        NativeBattleVector delta = new(unchecked((short)(anchor.X - original.X)), unchecked((short)(anchor.Y - original.Y)), unchecked((short)(anchor.Z - original.Z)));
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CE30(Vector(inputs.ReadModelArena9C(frame, actor), 12));
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(rotation, delta);
        NativeBattleVector position = new(unchecked((short)(anchor.X - rotated.X)), unchecked((short)(anchor.Y - rotated.Y)), unchecked((short)(anchor.Z - rotated.Z)));
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 28, PointView(position), 6));
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next < 30)
        {
            return new(frame, 0);
        }
        ushort flags = unchecked((ushort)((inputs.ReadModel(frame, actor).Flags0 & 0xEFDF) | BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, savedAddress, 2).Read(0, 2))));
        frame = WriteFlags174(frame, actor, flags, inputs);
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 28, ReadBytes(frame, savedAddress + 12, 8), 8));
        frame = inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 12, ReadBytes(frame, savedAddress + 4, 8), 8));
        return new(frame, 2);
    }

    private static NativeBattleEffect174Frame WriteFlags174(NativeBattleEffect174Frame frame, int actor, ushort flags, NativeBattleEffect174Inputs inputs) => inputs.Execute(frame, new NativeBattleEffect174WriteModelBytes(actor, 0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(flags)), 2));
}
