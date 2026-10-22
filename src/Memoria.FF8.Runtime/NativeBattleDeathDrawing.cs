using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDeathDraw(NativeBattleDeathFrame Frame, NativeBattleModulePoolTask Task, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleDeathDrawing
{
    public static NativeBattleDeathDraw Normal(NativeBattleDeathFrame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model, NativeBattleDeathResources resources, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleShadowSource shadow, NativeBattlePrimitiveCapabilities capabilities, NativeBattleSummonColors colors)
    {
        NativeBattleDeathFamilyProfile profile = RequireProfile(frame, resources);
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        int offset = profile.ModuleOffset(0x27419F0);
        const int length = 0xA08;
        NativeBattleEffectArena projected = NativeBattleEffectNodeContextMemory.Copy(state.Module, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        uint packet = checked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)(state.Cursor ?? throw new NotSupportedException("Native Death normal drawing requires its current private cursor.")));
        uint far = BinaryPrimitives.ReadUInt32LittleEndian(state.Module.Read(profile.ModuleOffset(0x273AFE4), 4));
        NativeBattleEffectOwnedModelRenderFrame current = new(frame.Memory, task, model, resources.Model, projected, profile.ModuleAddress(0x27419F0), length, far, packet);
        NativeBattleEffectOwnedModelRenderStep drawn = NativeBattleEffectOwnedModelRendering.DrawNormal(current, frame.Staging ?? throw new NotSupportedException("Native Death normal drawing requires its actual addressed model and temporary memory."), new(camera, projection, shadow, capabilities, colors));
        state = state with { Module = NativeBattleEffectNodeContextMemory.Copy(drawn.Frame.ProjectedScratch, 0, state.Module, offset, length) };
        return new(frame with { Memory = profile.WithState(drawn.Frame.Memory, state), Staging = drawn.Staging }, drawn.Frame.Task, drawn.Batch);
    }

    public static NativeBattleDeathDraw Dark(NativeBattleDeathFrame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model, NativeBattleDeathResources resources, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleShadowSource shadowSource, NativeBattlePrimitiveCapabilities capabilities)
    {
        _ = RequireProfile(frame, resources);
        return NativeBattleDeathModelDark.Draw(frame, task, model, resources, camera, projection, shadowSource, capabilities);
    }

    private static NativeBattleDeathFamilyProfile RequireProfile(NativeBattleDeathFrame frame, NativeBattleDeathResources resources)
    {
        NativeBattleDeathFamilyProfile profile = resources.Profile;
        if (frame.Effect.Binding.Effect != profile.Effect || frame.Effect.Binding.Entry != profile.Entry)
        {
            throw new InvalidDataException("Native Death drawing requires matching original module and resource identities.");
        }
        return profile;
    }

}
