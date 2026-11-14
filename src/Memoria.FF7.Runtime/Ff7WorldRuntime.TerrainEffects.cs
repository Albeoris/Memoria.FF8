using System.Numerics;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    private void SpawnWorldTerrainEffect(Ff7WorldEntity entity, int sequence, Vector3 offset, Vector3 velocity, int positionAngle = 0, int velocityAngle = 0, int gravity = 0, Vector3? origin = null)
    {
        SpawnWorldEffect(entity, sequence, offset, velocity, positionAngle, velocityAngle, gravity, entity.Direction + entity.TurnOffset, origin);
    }

    private void EmitWorldTerrainEffects()
    {
        if (mainState < 0 || controlledModel < 0)
        {
            return;
        }
        foreach (Ff7WorldEntity entity in ActiveWorldEntities().Where(entity => (entity.Flags & 10) == 2).ToArray())
        {
            EmitWorldTerrainEffect(entity);
        }
    }

    private void EmitWorldTerrainEffect(Ff7WorldEntity entity)
    {
        int terrain = entity.Terrain & 31;
        bool moving = (entity.Flags & 1) != 0;
        bool human = entity.Model is 0 or 1 or 2;
        bool bird = IsWorldChocobo(entity.Model);
        int frame = entity.PoseFrame;
        int direction = entity.Direction + entity.TurnOffset;
        Vector3 origin = new(entity.PreviousX, entity.PreviousY ?? entity.Y, entity.PreviousZ);
        Vector3 motion = new(entity.X - entity.PreviousX, 0, entity.Z - entity.PreviousZ);
        if (entity.Model == 3)
        {
            int area = PresentationArea;
            if (moving && area is 3 or 4 or 6 or 8 && (vehicle?.FlightState ?? 0) >= 0 && Math.Abs(motion.X) + Math.Abs(motion.Z) > 100)
            {
                uint counter = effects?.Counter ?? unchecked((uint)tick);
                int roll = vehicle?.FlightRoll ?? 0;
                int angle = roll * 4 + direction;
                Vector3 current = new(entity.X, entity.Y, entity.Z);
                SpawnWorldTerrainEffect(entity, (roll * 4 < 200 ? 0x24 : 0x27) + (int)(counter % 3), new(300, 400 - Math.Max(0, roll), -90), Vector3.Zero, angle, origin: current);
                SpawnWorldTerrainEffect(entity, 50 + (int)(counter % 6), new(300, 400 - Math.Max(0, roll), -90), Vector3.Zero, angle, origin: current);
                SpawnWorldTerrainEffect(entity, (roll * 4 > -200 ? 0x27 : 0x24) + (int)(counter % 3), new(-300, 400 + Math.Min(0, roll), -90), Vector3.Zero, angle, origin: current);
                SpawnWorldTerrainEffect(entity, 50 + (int)(counter % 6), new(-300, 400 + Math.Min(0, roll), -90), Vector3.Zero, angle, origin: current);
                SpawnWorldTerrainEffect(entity, 0x2A + (int)(counter % 3), new(0, 300, -300), Vector3.Zero, angle, origin: current);
                SpawnWorldTerrainEffect(entity, 50 + (int)(counter % 6), new(0, 300, -300), Vector3.Zero, angle, origin: current);
            }
            if (terrain is >= 3 and <= 7 && entity.Y < entity.GroundHeight + 1000 && frame % 3 == 0)
            {
                Vector3 velocity = new((int)motion.X >> 1, 50, (int)motion.Z >> 1);
                if (Math.Abs(velocity.X) + Math.Abs(velocity.Z) > 25)
                {
                    Vector3 surface = new(origin.X - velocity.X * 4, entity.GroundHeight, origin.Z - velocity.Z * 4);
                    SpawnWorldTerrainEffect(entity, 0, Vector3.Zero, velocity, velocityAngle: 0x700, origin: surface);
                    SpawnWorldTerrainEffect(entity, 1, Vector3.Zero, velocity, velocityAngle: 0x900, origin: surface);
                }
            }
            return;
        }
        if (moving && bird && frame is 2 or 14 && (0x01110701u & (1u << terrain)) != 0)
        {
            Vector3 previous = new(origin.X - motion.X, origin.Y + 100, origin.Z - motion.Z);
            SpawnWorldTerrainEffect(entity, terrain != 10 ? 6 : 5, Vector3.Zero, new(motion.X, 100, motion.Z), velocityAngle: 2048, gravity: 50, origin: previous);
            return;
        }
        if (terrain == 10 && Resources.World != 3 && moving)
        {
            if (human && frame is 1 or 8)
            {
                SpawnWorldTerrainEffect(entity, frame >> 3 != 0 ? 2 : 3, new(0, 0, 40), Vector3.Zero, (frame >> 3 != 0 ? 1024 : 3072) + direction);
            }
            else if (bird && frame is 1 or 13)
            {
                SpawnWorldTerrainEffect(entity, 4, new(0, 0, 120), Vector3.Zero, (frame >> 3 != 0 ? 3072 : 1024) + direction);
            }
            return;
        }
        if (human && ((terrain & 15) == 8 || terrain == 28) && frame is 1 or 8)
        {
            SpawnWorldTerrainEffect(entity, 8, Vector3.Zero, new(0, 20, 0));
            return;
        }
        if (entity.Model == 5 && (RequireEntity(controlledModel).Flags & 128) == 0)
        {
            if (moving && frame % 3 == 0)
            {
                SpawnWorldTerrainEffect(entity, 9, new(400, 0, 0), Vector3.Zero, direction);
                SpawnWorldTerrainEffect(entity, 9, new(-500, 0, 0), Vector3.Zero, direction);
            }
            return;
        }
        if (moving && (human || bird) && terrain is 1 or 25 && frame % 3 == 0)
        {
            SpawnWorldTerrainEffect(entity, 10, Vector3.Zero, Vector3.Zero, direction, origin: origin + new Vector3(0, 300, 0));
            return;
        }
        if (human && terrain == 7)
        {
            if (moving && frame is 1 or 8)
            {
                SpawnWorldTerrainEffect(entity, 12, Vector3.Zero, motion, velocityAngle: 2048);
            }
            return;
        }
        if (moving && bird && terrain is >= 3 and <= 7 && frame is 1 or 13)
        {
            SpawnWorldTerrainEffect(entity, 13, new(0, 0, -100), Vector3.Zero, direction);
            return;
        }
        if (moving && entity.Model == 6)
        {
            if (frame % 3 == 0)
            {
                if (terrain is 1 or 25)
                {
                    SpawnWorldTerrainEffect(entity, 14, Vector3.Zero, Vector3.Zero, origin: origin + new Vector3(0, 300, 0));
                }
                else
                {
                    Matrix4x4 rotation = Ff7WorldMath.Quantize(inputMath.Rotation(0, direction, 0) * inputMath.Rotation(vehicle?.BuggyTiltX ?? 0, 0, 0) * inputMath.Rotation(0, 0, vehicle?.BuggyTiltZ ?? 0));
                    foreach (int x in new[] { 150, -150 })
                    {
                        Vector3 offset = Vector3.Transform(new(x, 100, -300), rotation);
                        SpawnWorldTerrainEffect(entity, terrain != 4 ? 16 : 15, offset, new(0, 20, 0), gravity: 10);
                    }
                }
            }
            return;
        }
        if (moving && entity.Model == SubmarineModel)
        {
            if (Resources.World == 2 && (frame & 7) == 0)
            {
                SpawnWorldTerrainEffect(entity, 17, new(0, 200, -400), new(0, 50, 0), direction, direction);
            }
            else if (Resources.World == 0)
            {
                SpawnWorldTerrainEffect(entity, 0x2D + (int)((effects?.Counter ?? unchecked((uint)tick)) & 3), new(0, 240, -500), Vector3.Zero, direction);
            }
            return;
        }
        if (moving && entity.Model == 30 && (frame & 15) == 0)
        {
            SpawnWorldTerrainEffect(entity, 17, new(600, 1000, -400), new(0, 50, 0), direction, direction);
            SpawnWorldTerrainEffect(entity, 17, new(-600, 1000, -400), new(0, 50, 0), direction, direction);
            return;
        }
        if (moving && entity.Model == 8 && (frame & 3) == 0)
        {
            SpawnWorldTerrainEffect(entity, 18, new(0, 0, -700), Vector3.Zero, direction);
        }
    }
}
