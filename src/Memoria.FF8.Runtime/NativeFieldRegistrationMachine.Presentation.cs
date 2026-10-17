namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    public static NativeBattleRetainedMemory RestoreRetainedPresentation(NativeFieldRegistrationState state, NativeBattleRetainedMemory retained, IReadOnlyList<NativeFieldRetainedPresentation> actors)
    {
        if (!state.Instances.Select(instance => instance.ObjectId).SequenceEqual(actors.Select(actor => actor.ObjectId)))
        {
            throw new InvalidDataException("Native52B840 must restore the same retained physical actor order.");
        }
        NativeFieldRegistrationMemory memory = new(retained.FileStaging ?? throw new InvalidDataException("Native field registration lost its shared memory."), FieldArena, 0x1DCD6E4);
        Context context = new(memory, retained.EffectMemory ?? throw new InvalidDataException("Native field registration lost its shared VRAM."));
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
                RestoreEyes(context, address, actor.EyeMode);
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
        return retained with { FileStaging = memory.Finish(), EffectMemory = context.Effects };
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

    private static void RestoreEyes(Context context, uint instance, uint mode)
    {
        NativeFieldRegistrationMemory memory = context.Memory;
        uint resource = memory.Read32(instance);
        if ((memory.Read8(resource + 95) & 1) == 0)
        {
            return;
        }
        if (mode is not (0x100000 or 0x200000))
        {
            throw new NotSupportedException("Native retained blink mode requires its actual RNG and physical eye timer owners.");
        }
        uint eyes = memory.Read32(resource + 20);
        uint source = mode == 0x200000 ? eyes + 12 : checked(eyes + 8 + 4u * memory.Read8(eyes + 5));
        short sourceX = unchecked((short)memory.Read16(source));
        short sourceY = unchecked((short)memory.Read16(source + 2));
        short destinationX = unchecked((short)memory.Read16(eyes + 6));
        short destinationY = unchecked((short)memory.Read16(eyes + 8));
        short width = memory.Read8(eyes + 3);
        short height = memory.Read8(eyes + 4);
        NativeBattleEffectTextureState textures = context.Effects.Textures ?? throw new InvalidDataException("Native retained eye copy lost its shared VRAM.");
        NativeBattlePrimitiveProcess? process = context.Effects.PrimitiveProcess;
        if (process?.TextureWindowCache is { } cache)
        {
            process = process with { TextureWindowCache = cache.Invalidate464850(destinationX, destinationY, destinationX + width - 1, destinationY + height - 1, textures) };
        }
        NativeBattleEffectTextureDrain copied = (textures with { Count = 0 }).EnqueueCopy505EB0(sourceX, sourceY, width, height, destinationX, destinationY).Drain505D20();
        context.Effects = context.Effects with { Textures = textures with { VramRegions = copied.State.VramRegions }, PrimitiveProcess = process };
        context.Uploads.AddRange(copied.Uploads);
        memory.Write8(instance + 114, (byte)(memory.Read8(instance + 114) & 15 | (mode == 0x200000 ? 0x20 : 0x40)));
    }
}
