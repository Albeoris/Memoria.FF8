namespace Memoria.FF8.Runtime;

public static partial class NativeFieldRegistrationMachine
{
    public static NativeBattleFieldResourceReloadResult SetEyes532340(NativeFieldRegistrationState state, NativeBattleRetainedMemory retained, int objectId, uint mode, byte? blinkParameter, NativeCrtRandomState? crtRandom)
    {
        state.Validate(retained);
        NativeFieldRegisteredInstance instance = state.Instances.Single(item => item.ObjectId == objectId);
        if (instance.ModelId < 0)
        {
            return new(retained, crtRandom, crtRandom);
        }
        NativeFieldRegistrationMemory memory = new(retained.FileStaging!, FieldArena, 0x1DCD6E4);
        Context context = new(memory, retained.EffectMemory ?? throw new NotSupportedException("Native eyes require their current shared VRAM."));
        NativeCrtRandomState? random = crtRandom;
        RestoreEyes(context, instance.Address, mode, blinkParameter, ref random);
        return new(retained with { FileStaging = memory.Finish(), EffectMemory = context.Effects, ModelCpuResources = memory.ReconcileCpuBanks(retained.ModelCpuResources) }, crtRandom, random);
    }

    public static NativeBattleFieldResourceReloadResult AdvanceEyes530810(NativeFieldRegistrationState state, NativeBattleRetainedMemory retained, NativeCrtRandomState? crtRandom)
    {
        state.Validate(retained);
        if (!state.Instances.Any(instance => instance.ModelId >= 0 && (retained.FileStaging!.Read(instance.Address + 114, 1)[0] & 0x90) != 0))
        {
            return new(retained, crtRandom, crtRandom);
        }
        NativeFieldRegistrationMemory memory = new(retained.FileStaging!, FieldArena, 0x1DCD6E4);
        Context context = new(memory, retained.EffectMemory ?? throw new NotSupportedException("Native eye callbacks require their current shared VRAM."));
        NativeCrtRandomState? random = crtRandom;
        foreach (NativeFieldRegisteredInstance instance in state.Instances)
        {
            if (instance.ModelId < 0)
            {
                continue;
            }
            uint address = instance.Address;
            uint resource = memory.Read32(address);
            if ((memory.Read8(resource + 95) & 1) == 0)
            {
                continue;
            }
            uint eyes = memory.Read32(resource + 20);
            short period = memory.ReadSigned16(eyes + 10);
            byte mode = memory.Read8(address + 114);
            if ((mode & 0x80) != 0)
            {
                bool start = period <= 0;
                if (!start)
                {
                    NativeCrtRandomState current = random ?? throw new NotSupportedException("Native idle eyes require the current CRT random owner.");
                    random = current.Draw(out ushort value);
                    start = value % period == 0;
                }
                if (start)
                {
                    memory.Write8(address + 114, (byte)(mode & 15 | 0x10));
                }
            }
            if ((memory.Read8(address + 114) & 0x10) == 0)
            {
                continue;
            }
            byte countdown = unchecked((byte)(memory.Read8(address + 102) - 1));
            memory.Write8(address + 102, countdown);
            if (countdown != 0)
            {
                continue;
            }
            memory.Write8(address + 102, period >= 0 ? memory.Read8(address + 116) : unchecked((byte)period));
            byte frame = unchecked((byte)(memory.Read8(address + 103) - 1));
            memory.Write8(address + 103, frame);
            if (frame == 0)
            {
                memory.Write8(address + 114, (byte)(memory.Read8(address + 114) & 15 | 0x80));
                frame = memory.Read8(eyes + 5);
                memory.Write8(address + 103, frame);
            }
            CopyEyeRectangle(context, eyes, checked(eyes + 8 + 4u * frame));
        }
        return new(retained with { FileStaging = memory.Finish(), EffectMemory = context.Effects, ModelCpuResources = memory.ReconcileCpuBanks(retained.ModelCpuResources) }, crtRandom, random);
    }

    private static void CopyEyeRectangle(Context context, uint eyes, uint source)
    {
        NativeFieldRegistrationMemory memory = context.Memory;
        short sourceX = unchecked((short)memory.Read16(source));
        short sourceY = unchecked((short)memory.Read16(source + 2));
        short destinationX = unchecked((short)memory.Read16(eyes + 6));
        short destinationY = unchecked((short)memory.Read16(eyes + 8));
        short width = memory.Read8(eyes + 3);
        short height = memory.Read8(eyes + 4);
        NativeBattleEffectTextureState textures = context.Effects.Textures ?? throw new InvalidDataException("Native eye copy lost its shared VRAM.");
        NativeBattlePrimitiveProcess? process = context.Effects.PrimitiveProcess;
        if (process?.TextureWindowCache is { } cache)
        {
            process = process with { TextureWindowCache = cache.Invalidate464850(destinationX, destinationY, destinationX + width - 1, destinationY + height - 1, textures) };
        }
        NativeBattleEffectTextureDrain copied = (textures with { Count = 0 }).EnqueueCopy505EB0(sourceX, sourceY, width, height, destinationX, destinationY).Drain505D20();
        context.Effects = context.Effects with { Textures = textures with { VramRegions = copied.State.VramRegions }, PrimitiveProcess = process };
        context.Uploads.AddRange(copied.Uploads);
    }
}
