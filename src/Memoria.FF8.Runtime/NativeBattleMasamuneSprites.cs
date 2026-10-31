using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMasamuneSpriteStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleModulePacketBatch Batch);
public sealed record NativeBattleMasamuneParticleHead(NativeBattleEffectMemory Memory, NativeBattleEffectArena Scratch72);

public static class NativeBattleMasamuneSprites
{
    public static NativeBattleMasamuneParticleHead PrepareParticle(NativeBattleEffectMemory memory, NativeBattleEffectArena particles, int record, NativeBattleEffectArena scratch72)
    {
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Masamune requires its current geometry controls."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Masamune requires its current data registers."));
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Masamune requires its actual lighting controls.");
        NativeBattleActionMatrix matrix = lighting.LightMatrix?.ToActionMatrix() ?? throw new NotSupportedException("Masamune requires its current light matrix.");
        NativeBattleScriptedBackColor back = (lighting.BackColor ?? throw new NotSupportedException("Masamune requires its current raw backcolor translation.")).RequireKnown();
        matrix = matrix with { X = back.Red, Y = back.Green, Z = back.Blue };
        NativeBattleVector position = new(NativeBattleMasamuneParticles.Word(particles, record + 8), NativeBattleMasamuneParticles.Word(particles, record + 10), NativeBattleMasamuneParticles.Word(particles, record + 12));
        (byte[] bytes, bool[] known) = particles.ReadSnapshot(record + 14, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        step = NativeBattleGeometry.LoadVector(step, 0, position, w);
        NativeBattleGeometryRegisterStep calculation = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, null) };
        calculation = NativeBattleGeometry.Transform(calculation);
        step = step with { Colors = calculation.Colors };
        int[] values = step.Colors.Mac.Select(value => value ?? throw new InvalidDataException("Masamune light transform did not publish its full MAC.")).ToArray();
        var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(values[0], values[1], values[2]);
        int[] normalized = [direction.X, direction.Y, direction.Z];
        short size = NativeBattleMasamuneParticles.Word(particles, record + 6);
        int depth = -(size >> 3);
        for (int axis = 0; axis < 3; axis++)
        {
            scratch72 = NativeBattleStaticMeshContext88.Put(scratch72, 0x28 + axis * 4, unchecked((uint)normalized[axis]));
            values[axis] = unchecked(values[axis] + (unchecked(normalized[axis] * depth) >> 12));
            scratch72 = NativeBattleStaticMeshContext88.Put(scratch72, 0x38 + axis * 4, unchecked((uint)values[axis]));
        }
        NativeBattleActionMatrix scale = new([size, 0, 0, 0, size, 0, 0, 0, size], values[0], values[1], values[2], 4096);
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, scale, true, true, 0) };
        return new(memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, scratch72);
    }

    public static NativeBattleMasamuneSpriteStep Draw(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, uint temporary, NativeBattleModuleResource resource, ushort phase, short angle, bool ring, short size, NativeBattleCameraProjection projection, short cameraRoll)
    {
        return DrawWithPresentation(memory, staging, temporary, resource, phase, angle, ring, size, projection, cameraRoll, null);
    }

    public static NativeBattleMasamuneSpriteStep DrawWithPresentation(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, uint temporary, NativeBattleModuleResource resource, ushort phase, short angle, bool ring, short size, NativeBattleCameraProjection projection, short cameraRoll, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("Masamune sprites require their current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Masamune sprites require their current data registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Masamune sprites require their current projection controls.");
        NativeBattlePackedMatrix32 captured = NativeBattleModuleSpriteSetup.CaptureCurrentMatrix(step.Geometry);
        NativeBattleActionMatrix loaded = captured.ReadMatrix();
        NativeBattleActionMatrix spriteView = NativeBattleCuragaMatrices.Identity8DD770() with { X = loaded.X, Y = loaded.Y, Z = loaded.Z };
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0)
        {
            Resource = resource,
            PilotIdentity = pilotIdentity,
            PilotProjection = pilotIdentity.HasValue ? projection : null,
            SpriteFrame = resource.ProjectedFrame571C80(phase),
            Phase = phase,
            Anchor = new(0, 0, 0, 0),
            Scale = size,
            Rotation = angle,
            SpriteFlags = ring ? (ushort)8 : (ushort)9
        };
        staging = NativeBattleModuleSpriteTemporary.Begin571C80(staging, temporary, resource.Address, draw.SpriteFlags);
        if (!ring)
        {
            staging = Put(staging, temporary + 8, unchecked((uint)(int)angle));
        }
        NativeBattleModuleSpriteTemporaryResult emitted = NativeBattleModuleSpriteTemporary.Emit571C80(staging, temporary, draw, projection with { View = spriteView }, cameraRoll, 17, 2, captured);
        staging = emitted.Staging;
        NativeBattleActionMatrix main = NativeBattlePoseMatrices.ModuleSpriteCamera(draw, spriteView, cameraRoll);
        bool accepted = false;
        NativeBattleModuleSpriteFrame selected = draw.SpriteFrame!;
        for (int offset = 0; offset < selected.Quads.Length; offset += 20)
        {
            byte[] quad = selected.Quads.AsSpan().Slice(offset, 20).ToArray();
            NativeBattleActionMatrix local = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(NativeBattleCuragaMatrices.Identity8DD770(), quad);
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, true, false, Padding(staging, temporary + 150)) };
            for (int column = 0; column < 3; column++)
            {
                step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)local.Rotation[column]), unchecked((ushort)local.Rotation[3 + column]), unchecked((ushort)local.Rotation[6 + column]));
                step = NativeBattleGeometry.Transform(step, false, true);
            }
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, main, false, true, null) };
            NativeBattleVector displacement = new(unchecked((short)local.X), unchecked((short)local.Y), unchecked((short)local.Z));
            step = NativeBattleGeometry.LoadVector(step, 0, displacement, displacement.Z < 0 ? (short)-1 : (short)0);
            step = NativeBattleGeometry.Transform(step);
            NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(main, quad);
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, matrix, true, true, Padding(staging, temporary + 118)) };
            short width = unchecked((short)(8 * quad[0]));
            short height = unchecked((short)(8 * quad[2]));
            NativeBattleVector[] corners = [new(unchecked((short)-width), unchecked((short)-height), 0), new(width, unchecked((short)-height), 0), new(unchecked((short)-width), height, 0), new(width, height, 0)];
            for (int corner = 0; corner < 3; corner++)
            {
                step = NativeBattleGeometry.LoadVector(step, corner, corners[corner], Padding(staging, temporary + 74 + (uint)corner * 8));
            }
            step = NativeBattleGeometry.ProjectTriple(step, projection, controls.Dqa, controls.Dqb);
            if ((step.Colors.Flags!.Value & 0x60000) != 0)
            {
                continue;
            }
            step = NativeBattleGeometry.LoadVector(step, 0, corners[3], Padding(staging, temporary + 98));
            step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
            if (!accepted)
            {
                step = NativeBattleGeometry.Average45E610(step, controls.Zsf4 ?? throw new NotSupportedException("Masamune sprites require their actual ZSF4 control."));
            }
            accepted = true;
        }
        memory = memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors };
        return new(memory, staging, emitted.Batch);
    }

    internal static short? Padding(NativeBattleFileStaging staging, uint address)
    {
        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, address, 2);
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    internal static NativeBattleFileStaging Put(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}
