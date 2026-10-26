using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static NativeBattleEffect218Frame Advance(NativeBattleEffect218Frame frame, NativeBattleEffect218Resources resources, NativeBattleEffect218RuntimeInputs inputs)
    {
        RequireBinding(frame);
        frame.State.Validate();
        if (frame.Effect.ReturnedDescriptor == 0)
        {
            return frame;
        }
        if (frame.Effect.Pools.Length != Pools.Length)
        {
            throw new InvalidDataException("Effect218 requires its actual three physical task banks.");
        }
        for (int index = 0; index < Pools.Length; index++)
        {
            NativeBattleCureFamilyPool expected = Pools[index];
            NativeBattleEffectTaskPool actual = frame.Effect.Pools[index];
            if (actual.DescriptorAddress != expected.Descriptor || actual.BankAddress != expected.Bank || actual.Stride != expected.Stride || actual.Slots.Length != expected.Capacity)
            {
                throw new InvalidDataException("Effect218 current task bank differs from its original descriptor.");
            }
        }
        (frame, int survivors) = AdvancePool218(frame, 0, resources, inputs);
        frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = survivors == 0 ? 0u : RootDescriptor } };
        return survivors == 0 ? DetachBanks(frame) : frame;
    }

    private static (NativeBattleEffect218Frame Frame, int Survivors) AdvancePool218(NativeBattleEffect218Frame frame, int index, NativeBattleEffect218Resources resources, NativeBattleEffect218RuntimeInputs inputs)
    {
        NativeBattleEffect218Frame current = frame;
        NativeBattleEffectTaskStep step = frame.Effect.Pools[index].Advance((pool, slot) =>
        {
            current = WithPool(current, index, pool);
            (current, int result) = AdvanceTask218(current, checked(pool.BankAddress + (uint)(slot * pool.Stride)), resources, inputs);
            return new(current.Effect.Pools[index], result);
        });
        return (WithPool(current, index, step.Pool), step.Survivors);
    }

    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceTask218(NativeBattleEffect218Frame frame, uint address, NativeBattleEffect218Resources resources, NativeBattleEffect218RuntimeInputs inputs)
    {
        uint flags = inputs.ReadGraphicsFlags(frame);
        uint callback = Read(frame, address).Callback;
        return callback switch
        {
            RootCallback => AdvanceRoot6374D0(frame, address, resources, inputs),
            MainCallback => AdvanceMain6328A0(frame, address, flags, inputs.Main),
            0x632EB0 => AdvanceOpening632EB0(frame, address, flags, inputs.DeliverModule),
            0x633090 => AdvancePair633090(frame, address, flags, (current, selected) => DrawPair633090(current, selected, resources.Mesh(0xE21CC0), inputs.ReadProjection(current), inputs.Colors, inputs.ReadNormalDepth(current), inputs.Deliver)),
            0x633200 => AdvanceRay633200(frame, address, flags, resources, inputs.ReadCameraEye(frame), inputs.Main.ReadView(frame), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), inputs.ReadNormalDepth(frame), inputs.Deliver),
            0x633560 => AdvanceSpin633560(frame, address, flags, (current, selected) => DrawSpin633560(current, selected, resources.Mesh(0xE1B7E0), inputs.ReadProjection(current), inputs.Colors, inputs.Deliver)),
            0x633680 => AdvanceFixedScreen633680(frame, address, flags, inputs.DeliverModule),
            0x633720 => AdvanceTargetWindow633720(frame, address, flags, resources.Mesh(0xE22570), inputs.Main.ReadView(frame), inputs.ReadProjection(frame), inputs.Colors, inputs.Deliver),
            0x6338B0 => AdvanceFlare6338B0(frame, address, flags, resources, inputs.Main.ReadView(frame), inputs.ReadProjection(frame), inputs.Colors, inputs.ReadBone502170, inputs.Deliver),
            0x633AC0 => AdvanceShadow633AC0(frame, address, flags, resources.Mesh(0xE254D8), inputs.ReadProjection(frame), inputs.Colors, inputs.Deliver),
            0x633BF0 => AdvanceCleanup633BF0(frame, address, inputs.RestoreCommon508630),
            0x633C30 => AdvanceScene633C30(frame, address, flags, resources, inputs.Colors),
            0x6340A0 => AdvanceShaft6340A0(frame, address, flags, resources, inputs.ReadProjection(frame), inputs.Colors, inputs.ReadBone502170, inputs.Deliver),
            0x634300 => AdvanceRadial634300(frame, address, flags, resources, inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), inputs.Deliver),
            0x634750 or 0x634890 or 0x634A10 => AdvanceBurst218(frame, address, flags, (current, selected) => DrawBurst218(current, selected, resources.Mesh(BurstProfile218(Read(current, selected).Callback).Mesh), inputs.ReadProjection(current), inputs.Colors, inputs.Deliver)),
            0x634B90 => AdvanceCamera634B90(frame, address, flags, inputs.Main),
            0x634C00 => AdvanceFragmentDriver634C00(frame, address, flags, resources, inputs.ReadBone502170, (current, point) => NativeBattleEffect218Capture.Capture634E40(current, point, inputs.ReadCaptureServices(current))),
            0x635110 => AdvanceFragment635110(frame, address, flags, (current, selected) => DrawFragment635110(current, selected, resources, inputs.Main.ReadView(current), inputs.ReadProjection(current), inputs.Colors, inputs.Deliver)),
            0x636CA0 => AdvanceDebrisSprites636CA0(frame, address, flags, resources, inputs.Main.ReadView(frame), inputs.ReadProjection(frame), inputs.ReadCameraRoll(frame), inputs.Deliver),
            0x636EB0 => AdvanceMeshParticles636EB0(frame, address, flags, resources.Mesh(0xE1F5F0), inputs.Main.ReadView(frame), inputs.ReadProjection(frame), inputs.Colors, inputs.Deliver),
            0x637200 => AdvanceScreenWindow637200(frame, address, flags, resources.Mesh(0xE24FC8), inputs.ReadProjection(frame), inputs.Colors, inputs.ReadNormalDepth(frame), inputs.Deliver),
            0x637350 => AdvanceModels637350(frame, address, flags, resources, (current, model) => DrawOwnedModel637430(current, model, model == EmbeddedModelAddress ? resources.EmbeddedModel : resources.StreamedModel, inputs.ReadProjection(current), inputs.Colors, inputs.Hardware, inputs.ReadPrimitiveFlags(current), inputs.ReadCommonCursor(current), inputs.Deliver)),
            0x63E9C0 => AdvanceGuardianCamera63E9C0(frame, address, resources, inputs),
            _ => throw new NotSupportedException($"Effect218 reached unsupported native task callback {callback:X8}.")
        };
    }

    private static (NativeBattleEffect218Frame Frame, int Result) AdvanceRoot6374D0(NativeBattleEffect218Frame frame, uint address, NativeBattleEffect218Resources resources, NativeBattleEffect218RuntimeInputs inputs)
    {
        (frame, int main) = AdvancePool218(frame, 1, resources, inputs);
        (frame, int secondary) = AdvancePool218(frame, 2, resources, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, (main | secondary) == 0 ? 2 : 0);
    }
}
