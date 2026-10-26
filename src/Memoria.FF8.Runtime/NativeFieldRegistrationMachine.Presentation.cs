namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    public static NativeBattleFieldResourceReloadResult RestoreRetainedPresentation(NativeFieldRegistrationState state, NativeBattleRetainedMemory retained, IReadOnlyList<NativeFieldRetainedPresentation> actors, NativeCrtRandomState? crtRandom = null)
    {
        if (!state.Instances.Select(instance => instance.ObjectId).SequenceEqual(actors.Select(actor => actor.ObjectId)))
        {
            throw new InvalidDataException("Native52B840 must restore the same retained physical actor order.");
        }
        NativeFieldRegistrationMemory memory = new(retained.FileStaging ?? throw new InvalidDataException("Native field registration lost its shared memory."), FieldArena, 0x1DCD6E4);
        Context context = new(memory, retained.EffectMemory ?? throw new InvalidDataException("Native field registration lost its shared VRAM."), retained.FieldTextureAnimations);
        NativeCrtRandomState? random = crtRandom;
        for (int index = 0; index < actors.Count; index++)
        {
            NativeFieldRetainedPresentation actor = actors[index];
            NativeFieldRegisteredInstance instance = state.Instances[index];
            uint address = instance.Address;
            byte animation = actor.AutoAnimation ? actor.BaseAnimation : actor.Animation;
            if (instance.ModelId >= 0)
            {
                SelectAnimation532890(memory, address, animation);
            }
            if (actor.AutoAnimation)
            {
                memory.Write16(address + 82, 0);
            }
            else if (actor.Frame is ushort phase)
            {
                memory.Write16(address + 82, phase);
            }
            else
            {
                memory.Forget(address + 82, 2);
            }
            if (instance.ModelId >= 0 && actor.EyeMode != 0)
            {
                RestoreEyes(context, address, actor.EyeMode, actor.EyeBlinkParameter611, ref random);
            }
            memory.Write8(address + 96, actor.Visible ? (byte)0 : (byte)1);
            memory.Write16(address + 98, unchecked((ushort)actor.RootTranslationZ));
            memory.Write8(address + 97, actor.RendererMode);
            if (instance.ModelId < 0)
            {
                continue;
            }
            uint resource = memory.Read32(address);
            for (uint buffer = 0; buffer < 2; buffer++)
            {
                uint packet = memory.Read32(address + 88 + buffer * 4);
                int[] strides = [28, 36, 32, 40];
                for (uint type = 0; type < 4; type++)
                {
                    uint count = memory.Read32(resource + 48 + type * 4);
                    for (uint primitive = 0; primitive < count; primitive++)
                    {
                        memory.Write8(packet + 4, actor.Red);
                        memory.Write8(packet + 5, actor.Green);
                        memory.Write8(packet + 6, actor.Blue);
                        if (type < 2)
                        {
                            for (uint color = 12; color <= 20; color += 8)
                            {
                                memory.Write8(packet + color, actor.Red);
                                memory.Write8(packet + color + 1, actor.Green);
                                memory.Write8(packet + color + 2, actor.Blue);
                            }
                        }
                        if (actor.PrimitiveTransparency)
                        {
                            memory.Write8(packet + 7, (byte)(memory.Read8(packet + 7) | 2));
                        }
                        packet = checked(packet + (uint)strides[type]);
                    }
                }
            }
            if (actor.AdditionalControlDisabled)
            {
                memory.Write8(address + 96, (byte)(memory.Read8(address + 96) | 0x20));
                memory.Fill(0x1DC400C, 0x2004, 255);
                memory.Write32(0x1DC6010, uint.MaxValue);
            }
        }
        return new(retained with { FileStaging = memory.Finish(), EffectMemory = context.Effects, ModelCpuResources = memory.ReconcileCpuBanks(retained.ModelCpuResources), FieldTextureAnimations = context.Animations }, crtRandom, random);
    }

    private static void SelectAnimation532890(NativeFieldRegistrationMemory memory, uint instance, byte animation)
    {
        if (animation >= memory.Read16(instance + 100))
        {
            throw new InvalidDataException("Native retained animation exceeds its reloaded clip bank.");
        }
        uint clips = memory.Read32(instance + 104);
        uint units = 0;
        for (uint clip = 0; clip < animation; clip++)
        {
            units = checked(units + (uint)memory.Read16(clips + clip * 4) * (uint)(memory.Read16(clips + clip * 4 + 2) + 1));
        }
        memory.Write16(instance + 126, animation);
        memory.Write16(instance + 12, unchecked((ushort)(16 * memory.Read16(clips + (uint)animation * 4))));
        memory.Write16(instance + 14, memory.Read16(clips + (uint)animation * 4 + 2));
        memory.Write32(instance + 8, checked(memory.Read32(instance + 108) + units * 4));
        memory.Write16(instance + 80, 0);
        memory.Write32(instance + 120, uint.MaxValue);
        memory.Write16(instance + 124, ushort.MaxValue);
    }

    private static void RestoreEyes(Context context, uint instance, uint mode, byte? blinkParameter, ref NativeCrtRandomState? random)
    {
        NativeFieldRegistrationMemory memory = context.Memory;
        uint resource = memory.Read32(instance);
        if ((memory.Read8(resource + 95) & 1) == 0)
        {
            return;
        }
        uint eyes = memory.Read32(resource + 20);
        if (mode == 0x80000)
        {
            if (memory.ReadSigned16(eyes + 10) < 0)
            {
                memory.Copy(eyes + 10, instance + 102, 1);
                memory.Copy(eyes + 10, instance + 116, 1);
            }
            else
            {
                byte parameter = blinkParameter ?? throw new NotSupportedException("Native retained blink requires its actual actor BYTE611.");
                NativeCrtRandomState current = random ?? throw new NotSupportedException("Native retained blink requires the current55CBD2 CRT random owner.");
                memory.Write16(eyes + 10, parameter);
                current = current.Draw(out ushort first);
                current = current.Draw(out ushort second);
                random = current;
                memory.Write8(instance + 102, (byte)(first % 8 + 1));
                memory.Write8(instance + 116, (byte)(second % 8 + 1));
            }
            memory.Write8(instance + 114, (byte)(memory.Read8(instance + 114) & 15 | 0x10));
            memory.Copy(eyes + 5, instance + 103, 1);
            return;
        }
        if (mode is not (0x100000 or 0x200000))
        {
            throw new InvalidDataException("Native retained eye mode is outside its implemented physical mask.");
        }
        uint source = mode == 0x200000 ? eyes + 12 : checked(eyes + 8 + 4u * memory.Read8(eyes + 5));
        CopyEyeRectangle(context, eyes, source, mode == 0x200000);
        memory.Write8(instance + 114, (byte)(memory.Read8(instance + 114) & 15 | (mode == 0x200000 ? 0x20 : 0x40)));
    }
}
