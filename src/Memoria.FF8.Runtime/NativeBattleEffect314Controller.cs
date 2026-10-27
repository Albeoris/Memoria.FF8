using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame Initialize(NativeBattleEffect314Frame frame, NativeBattleEffect314Resources resources, NativeBattleEffect314Inputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        resources.Validate();
        if (!frame.Effect.Pools.IsEmpty)
        {
            throw new InvalidDataException("Effect314 requires inactive task banks before entry.");
        }
        int offset = frame.Effect.ResourcePointers.SingleOrDefault(pointer => pointer.GlobalAddress == InitializerGlobal)?.ArenaOffset ?? throw new NotSupportedException("Effect314 requires its executed texture initializer.");
        uint texture = checked(CommonAddress + (uint)offset);
        frame = frame with { State = frame.State.WriteDword(InitializerGlobal, texture) };
        frame = inputs.Execute(frame, new NativeBattleEffect314UploadTim505E30(texture));
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect314 requires its actual current parameters.");
        uint model = ModelAddress(parameters.Actor78);
        _ = ModelAddress(parameters.Actor78 + 1);
        frame = frame with { State = frame.State.WriteDword(ParametersGlobal, 0x1D99A78).WriteDword(CasterGlobal, model).WriteDword(WorkGlobal, CommonAddress) };
        frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model, 2, 0xFFF3, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(model + 156, 2, 0xFFF3, 0));
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1);
        (root, _) = root.Append(RootCallback);
        frame = frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = RootDescriptor }, State = frame.State.DetachBank(RootBank) };
        frame = ClearDwords6DA380(frame, RootBank + 12, 2);
        for (int physical = 0; physical < 3; physical++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect314ModelFlags(ModelAddress(physical), 2, ushort.MaxValue, 4));
        }
        return inputs.Execute(frame, new NativeBattleEffect314RegisterCamera5099A0(CameraAddress));
    }

    public static NativeBattleEffect314Frame InitializeChildBanks(NativeBattleEffect314Frame frame)
    {
        if (frame.Effect.Pools.Length != 1 || frame.State.ReadDword(WorkGlobal) != CommonAddress)
        {
            throw new InvalidDataException("Effect314 requires its inactive original COMMON child banks.");
        }
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, CommonAddress + MainOffset, MainStride, 1);
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, CommonAddress, ParticleStride, ParticleCapacity);
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools.Add(main).Add(particles);
        return frame with { Effect = frame.Effect with { Pools = pools }, Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, pools) };
    }

    private static void RequireBinding(NativeBattleEffect314Frame frame)
    {
        if (frame.Effect.RequestedEffect != Effect || frame.Effect.Binding.Entry != Entry || frame.Effect.Binding.Initializer != Initializer)
        {
            throw new InvalidDataException("Effect314 requires its actual selected entry.");
        }
    }

    private static uint ModelAddress(int physical) => (uint)physical < 7 ? checked(0x1D972C0u + (uint)(physical * 156)) : throw new InvalidDataException("Effect314 requires its actual consecutive physical model owners.");
}
