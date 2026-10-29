namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89MeshDrawing
{
    public static NativeBattleRinoa89ParticleDraw DrawBillboard6FD340(NativeBattleRinoa89Frame frame, NativeBattleVector point, ushort scale, short depthDisplacement, short phase, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleRinoa89Resources resources, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (phase < 0)
        {
            throw new InvalidDataException("Native6FD340 requires its actual nonnegative sprite frame.");
        }
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6FD340 requires its actual camera matrix.");
        NativeBattleModuleResource resource = new(0, 0x14A0538, 0, 0, Resource(resources, 0x14A0538));
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            SpriteFrame = resource.ProjectedFrame571C80(phase),
            Phase = checked((ushort)phase),
            Anchor = new(point.X, point.Y, point.Z, 0),
            Scale = unchecked((short)scale),
            DepthOffset = depthDisplacement,
            SpriteFlags = 0,
            PilotIdentity = pilotIdentity
        };
        NativeBattleActionMatrix billboard = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, view, 0);
        NativeBattleFileStaging staging = frame.Staging;
        staging = Word(Word(Word(staging, 0x21DFED0, scale), 0x21DFED8, scale), 0x21DFEE0, scale);
        staging = Dword(Dword(Dword(staging, 0x21DFEE4, unchecked((uint)billboard.X)), 0x21DFEE8, unchecked((uint)billboard.Y)), 0x21DFEEC, unchecked((uint)billboard.Z));
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 180);
        uint temporary = allocation.Address;
        staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, temporary, 0x14A0538, 0);
        NativeBattleModuleSpriteTemporaryResult result = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary, draw, projection, cameraRoll, 17, 2);
        NativeBattleGfGeometryBatch batch = result.Batch.CapturedWrites ?? throw new InvalidDataException("Native6FD340 lacks its actual sprite packet writes.");
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(result.Staging, 180) }, batch);
    }
}
