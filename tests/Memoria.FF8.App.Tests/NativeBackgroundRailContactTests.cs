using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBackgroundRailContactTests
{
    [Theory]
    [InlineData(0, 32)]
    [InlineData(38, 64)]
    [InlineData(48, 64)]
    [InlineData(50, 256)]
    public void ActualRailCollisionRollsBackForEverySelectedBodyAndRestoresItsNextCallback(int entity, int radius)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(0);
        NativeTrainRailBank bank = NativeTrainRailBank.Create(definition, 1);
        NativeWorldFootCameraState camera = CameraAt(bank.Nodes[0].Position);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        NativeWorldPathPosition body = bank.Nodes[0].Position;
        NativeWorldPathWords player = projection.RelativeWords(body);
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(entity));
        Assert.Equal(radius, NativeWorldTerrain.ProbeRadius(entity));
        for (long callback = 1; callback < 30; callback++)
        {
            bank = Project(bank, definition, callback, camera, projection);
        }
        Assert.Equal((short)1, bank.Pause);
        NativeTrainRailBank preceding = bank;
        int contacts = 0;
        int? speed = NativeNormalFootObjectProcess.SelectSpeed(preceding, 0, WorldStationContactCertificate.ContactRadius(selected));
        NativeTrainRailBank collided = preceding.Step(definition, 30, projection.Camera, camera.Origin, (node, _, _) => projection.ProjectNode(node), false, out _, next =>
        {
            if (next.Pause == 0 && WorldStationContactCertificate.EvaluateTrain(assets, next, definition, player, body.Y, projection.IsNear, probeRadius: radius) >= 0)
            {
                contacts++;
                return next.ResolveContact(preceding, definition, 30, projection.Camera, camera.Origin, (node, _, _) => projection.ProjectNode(node), false);
            }
            return next;
        }, speed);
        Assert.Equal(1, contacts);
        Assert.Equal((short)15, collided.Pause);
        Assert.Equal(0, collided.Speed);
        Assert.True(preceding.Nodes.Select(node => node.Position).SequenceEqual(collided.Nodes.Select(node => node.Position)));
        NativeTrainRailBank restored = JsonSerializer.Deserialize<NativeTrainRailBank>(JsonSerializer.Serialize(collided))!;
        restored.Validate(definition);
        NativeTrainRailBank nextOriginal = Project(collided, definition, 31, camera, projection);
        NativeTrainRailBank nextRestored = Project(restored, definition, 31, camera, projection);
        Assert.Equal((short)14, nextOriginal.Pause);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(nextOriginal), JsonSerializer.SerializeToElement(nextRestored)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(38, true)]
    [InlineData(48, true)]
    [InlineData(50, true)]
    public void ActualCarriageEdgeDistinguishesTheSelectedFootAndVehicleFootprints(int entity, bool expectedContact)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(0);
        NativeTrainRailBank bank = NativeTrainRailBank.Create(definition, 1);
        NativeWorldFootCameraState camera = CameraAt(bank.Nodes[0].Position);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        bank = Project(bank, definition, 1, camera, projection);
        int index = Enumerable.Range(0, bank.Presentation.Length).First(index => NativeTrainRailBank.Link(index) != 0 && bank.Presentation[index].Angles is not null);
        NativeWorldPathNodePresentation pose = bank.Presentation[index];
        NativeWorldPathNodeProjection anchor = pose.Projection!;
        NativeWorldContactShape shape = assets.ReadNativeContactShape(bank.Shape(definition, index));
        short localX = checked((short)(shape.Kind == 2 ? -(shape.Length >> 1) : 0));
        short localZ = checked((short)((shape.Width >> 1) + 48));
        (int x, int z) = NativeGardenTransform.Rotate(localX, localZ, pose.Angles!.Yaw);
        NativeWorldPathWords player = new(unchecked((short)(anchor.Words.X + x)), anchor.Words.Y, unchecked((short)(anchor.Words.Z + z)));
        // Isolate this actual projected carriage's native proximity word so that
        //a neighboring linked carriage cannot hide a selected-radius regression.
        int hit = WorldStationContactCertificate.EvaluateTrain(assets, bank, definition, player, anchor.Source.Y, position => position == anchor.Source, probeRadius: NativeWorldTerrain.ProbeRadius(entity));
        Assert.Equal(expectedContact, hit >= 0);
    }

    [Theory]
    [InlineData(38)]
    [InlineData(48)]
    [InlineData(50)]
    public void BrakingUsesOriginalSelectedModelDimensionsRatherThanTheFootModel(int entity)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainRailBank bank = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1) with { Pause = 0, Speed = 40 };
        ushort footRadius = WorldStationContactCertificate.ContactRadius(assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(0)));
        ushort selectedRadius = WorldStationContactCertificate.ContactRadius(assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(entity)));
        Assert.True(selectedRadius > footRadius);
        // At the same observed distance the foot can clear the next rail step,
        //while each wider original vehicle requires the train to stop.
        Assert.True(NativeNormalFootObjectProcess.SelectSpeed(bank, 100000, footRadius) > 0);
        Assert.Equal(0, NativeNormalFootObjectProcess.SelectSpeed(bank, 100000, selectedRadius));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlledGardenExcludesTheObjectScanRailTailWhileTheRailKernelStillDetectsItsBody(bool forceCircle)
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        using GameAssets game = new(TestResources.GameDirectory);
        NativeTrainRailBank primary = NativeTrainRailBank.Create(assets.ReadScriptedPath(0), 1);
        NativeWorldFootCameraState camera = CameraAt(primary.Nodes[0].Position);
        NativeWorldPathProjectionCertificate projection = NativeWorldPathProjectionCertificate.CreateFoot(camera);
        primary = Project(primary, assets.ReadScriptedPath(0), 1, camera, projection);
        NativeTrainRailBank alternate = Project(NativeTrainRailBank.Create(assets.ReadScriptedPath(1), 1), assets.ReadScriptedPath(1), 1, camera, projection);
        NativeWorldBackgroundRailState rails = new(1, 0, 1, primary, alternate, camera, 0, uint.MaxValue) { DistancePosition = camera.Origin };
        rails.Validate();
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750).WriteBytes(264, 1, 8).WriteBytes(265, 1, 0);
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(3, 48, -1), entry, NativeEventMemory.Empty, NativeSavedWorldState.CreateNewGame(), game.ReadNewGameCharacterVitals().AcquireGuardianForce(13));
        NativeWorldPathWords player = projection.RelativeWords(camera.Origin);
        NativeWorldContactShape selected = assets.ReadNativeContactShape(WorldAssets.NativeGardenEntityShape(48));
        Assert.Equal(allocation.Objects.Length, WorldStationContactCertificate.EvaluateRailTail(assets, rails, allocation.Objects.Length, player, camera.NativeHeight, projection.IsNear, probeRadius: 64, forceCircle: forceCircle, selectedShape: selected));
        IReadOnlyList<WorldNativeGardenProbe> probes = WorldAssets.CreateNativeGroundFootprint(camera.X, camera.RawZ, checked((short)camera.NativeHeight), 64);
        int contact = WorldGardenContactCertificate.Evaluate(assets, allocation, allocation.Objects, 48, 48, probes, projection, forceCircle: forceCircle, rails: rails);
        Assert.InRange(contact, -1, allocation.Objects.Length - 1);
    }

    private static NativeWorldFootCameraState CameraAt(NativeWorldPathPosition body)
    {
        return new(body.X, -body.Z, body.Y, body.X, -body.Z, body.Y - 256, 0, -112, -341, -7168);
    }

    private static NativeTrainRailBank Project(NativeTrainRailBank bank, NativeWorldScriptedPathDefinition definition, long callback, NativeWorldFootCameraState camera, NativeWorldPathProjectionCertificate projection)
    {
        return bank.Step(definition, callback, projection.Camera, camera.Origin, (node, _, _) => projection.ProjectNode(node), false, out _);
    }
}
