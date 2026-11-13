using System.Numerics;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private const int SubmarineModel = 13;
    private const uint SubmarineTerrainMask = 0x04048008;
    private const int SubmarineEntryDepth = -3000;
    private const int SubmarineDiveLimit = -5000;
    private const int SubmarineBottomClearance = 200;
    private const int SubmarineStep = 60;
    private int? submarineTint;

    private void InitializeUnderwaterVehicle()
    {
        if (Resources.World != 2)
        {
            return;
        }
        if (entryModule != 0 && persistent[0x3FD] == SubmarineModel && entities.ContainsKey(SubmarineModel))
        {
            controlledModel = SubmarineModel;
        }
        if (controlledModel == SubmarineModel)
        {
            Ff7WorldEntity submarine = RequireEntity(SubmarineModel);
            UpdateEntity(submarine with { Y = SubmarineEntryDepth, Flags = submarine.Flags | 128 });
            input = input with { CollisionRecovery = 0 };
        }
    }

    private void AdvanceUnderwaterSubmarineInput(Ff7WorldEntity submarine)
    {
        if (!submarine.TerrainResolved && tick == 0)
        {
            return;
        }
        ushort held = WorldControlHeld();
        ushort pressed = (ushort)(held & ~input.PreviousControlHeld);
        int turn = (held & 0x5000) != 0 ? 16 : 32;
        int left = ((held & 4) != 0 ? 1 : 0) + ((held & 0x8000) != 0 ? 1 : 0);
        int right = ((held & 8) != 0 ? 1 : 0) + ((held & 0x2000) != 0 ? 1 : 0);
        int heading = (input.CameraHeadingTarget ?? camera.Heading) - (turn * left >> ((held & 4) == 0 ? 1 : 0)) + (turn * right >> ((held & 8) == 0 ? 1 : 0));
        heading &= 4095;
        if ((pressed & 0x40) != 0 && (submarine.Terrain & 31) == 3)
        {
            throw new NotSupportedException("FFVII surfacing requires the surface submarine transport contract.");
        }
        if ((pressed & 0x900) != 0)
        {
            radarMode = radarMode == 0 ? 2 : 0;
        }
        if (left != 0 || right != 0)
        {
            WorldRandom.Next();
        }
        input = input with { CameraHeadingTarget = heading, PreviousControlHeld = held };

        int y = submarine.Y;
        if ((held & 0x1000) != 0 && y > SubmarineDiveLimit && submarine.TerrainResolved && y > submarine.GroundHeight + SubmarineBottomClearance)
        {
            y -= SubmarineStep;
        }
        if ((held & 0x4000) != 0 && submarine.Y < SubmarineEntryDepth)
        {
            y += SubmarineStep;
        }
        int speed = (held & 0x20) != 0 ? SubmarineStep * movementSpeedPercent / 100 : 0;
        Vector3 step = inputMath.TransformRotation(new(0, 0, -speed), 0, -heading, 0);
        int x = Wrap(submarine.X + (int)step.X, 0x48000);
        int z = Wrap(submarine.Z + (int)step.Z, 0x38000);
        bool moved = submarine.X != x || submarine.Z != z;
        UpdateEntity(submarine with { X = x, Y = y, Z = z, TargetDirection = (2048 - heading) & 4095, ScriptDirection = null, Flags = submarine.Flags | (moved ? 1 : 0) });
    }

    private void ResolveUnderwaterSubmarineTerrain(Ff7WorldEntity submarine)
    {
        FootCollisionResult? collision = ResolveFootCollision(submarine);
        if (collision is { } bottom)
        {
            bool moved = bottom.X != input.PreviousX || bottom.Z != input.PreviousZ;
            int turn = moved ? (submarine.TurnOffset * 7 + bottom.Angle) >> 3 : (submarine.TurnOffset + bottom.Angle) >> 1;
            submarine = submarine with { X = bottom.X, Z = bottom.Z, GroundHeight = bottom.Height, Terrain = bottom.Terrain, TerrainResolved = true, TurnOffset = turn, Flags = (submarine.Flags | 2) & ~1 | (moved ? 1 : 0) };
            input = input with { CollisionRecovery = 0, CollisionAge = bottom.Side == input.CollisionSide ? input.CollisionAge : 0, CollisionSide = bottom.Side };
        }
        else
        {
            submarine = submarine with { X = input.PreviousX, Y = (submarine.Flags & 4) != 0 ? submarine.Y : input.PreviousY, Z = input.PreviousZ, Flags = (submarine.Flags | 2) & ~1 };
            if (input.CollisionRecovery == 2)
            {
                input = input with { CollisionRecovery = 1 };
            }
        }
        input = input with { CollisionAge = unchecked(input.CollisionAge + 1), BridgeCooldown = Math.Max(0, input.BridgeCooldown - 1) };

        if (submarine.TerrainResolved && submarine.Y - submarine.GroundHeight < 500 && (submarine.Flags & 8) == 0 && pendingWorldBattle is null)
        {
            int script = submarine.Terrain >> 5 & 7;
            if (script >= 3 && input.TerrainScript != script)
            {
                Machine.StartMesh(submarine.X >> 13, submarine.Z >> 13, script - 3);
            }
            input = input with { TerrainScript = script >= 3 ? script : 0 };
        }
        if (submarine.TerrainResolved && submarine.Y <= submarine.GroundHeight + SubmarineBottomClearance)
        {
            submarine = submarine with { Y = Math.Min(SubmarineEntryDepth, submarine.GroundHeight + SubmarineBottomClearance) };
        }
        submarine = TurnWorldPlayer(submarine, submarine.TargetDirection);
        if (mainState >= 0 && (submarine.Flags & 8) == 0)
        {
            if (submarine.Animation == 0)
            {
                submarine = submarine with { FrameDivisor = (submarine.Flags & 1) != 0 ? 32 : 0 };
            }
            submarine = AdvanceWorldModelAnimation(submarine);
        }
        UpdateEntity(submarine);
    }
}
