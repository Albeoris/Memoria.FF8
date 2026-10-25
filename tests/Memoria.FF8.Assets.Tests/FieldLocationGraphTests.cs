using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets.Localization;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldLocationGraphTests
{
    [Fact]
    public void FollowsOutgoingEdgesAndPreservesEveryNearestNameTie()
    {
        FieldLocationSource[] fields = [Source(0, Jump(1).Concat(Jump(2))), Source(1, SetPlace(2)), Source(2, SetPlace(1)), Source(3, Jump(0)), Source(4, [])];
        FieldLocationGraphResult graph = FieldLocationGraph.Build(fields, ["zero", "one", "two"]);
        FieldLocationResolution root = graph.Resolutions.Single(row => row.FieldId == 0);
        Assert.Equal(FieldLocationStatus.Ambiguous, root.Status);
        Assert.Equal(1, root.Distance);
        Assert.Equal([1, 2], root.PlaceIds);
        Assert.Equal(["one", "two"], root.Names);
        Assert.Equal([1, 2], root.SetterFieldIds);
        Assert.Equal(2, graph.Resolutions.Single(row => row.FieldId == 3).Distance);
        Assert.Equal(FieldLocationStatus.NoPath, graph.Resolutions.Single(row => row.FieldId == 4).Status);
        Assert.Equal(FieldLocationStatus.Direct, graph.Resolutions.Single(row => row.FieldId == 1).Status);
        Assert.Equal(graph.Fingerprint, FieldLocationGraph.Build(fields.Reverse(), ["zero", "one", "two"]).Fingerprint);
        Assert.NotEqual(graph.Fingerprint, FieldLocationGraph.Build(fields, ["zero", "changed", "two"]).Fingerprint);
    }

    [Fact]
    public void UsesNearestSetterAndKeepsDifferentSetterFieldsForSamePlace()
    {
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, Jump(1).Concat(Jump(2))), Source(1, SetPlace(1).Concat(Jump(3))), Source(2, SetPlace(1)), Source(3, SetPlace(2))], ["zero", "one", "two"]);
        FieldLocationResolution root = graph.Resolutions[0];
        Assert.Equal(FieldLocationStatus.Nearest, root.Status);
        Assert.Equal([1], root.PlaceIds);
        Assert.Equal([1, 2], root.SetterFieldIds);
        Assert.Equal([1], graph.Resolutions[1].PlaceIds);
    }

    [Fact]
    public void MissingDynamicAndUnknownDestinationsRemainExplicit()
    {
        int[] dynamicJump = [Encode(Jsm.Opcode.PSHM_W, 100), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.MAPJUMP)];
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, Jump(1)), Source(1, dynamicJump), new(2, "missing", null), Source(3, Jump(99)), Source(4, SetPlace(99)), Source(5, [])], ["zero"]);
        Assert.True(graph.Resolutions[0].HasDynamicOperations);
        Assert.True(graph.Resolutions[0].Incomplete);
        Assert.Null(Assert.Single(graph.Nodes[1].Transitions).Value);
        Assert.Equal(FieldLocationStatus.MissingScript, graph.Resolutions[2].Status);
        Assert.True(graph.Resolutions[3].Incomplete);
        Assert.True(graph.Resolutions[4].Incomplete);
        Assert.Empty(graph.Resolutions[4].Names);
        Assert.Equal(FieldLocationStatus.NoPath, graph.Resolutions[5].Status);
        Assert.False(graph.Resolutions[5].Incomplete);
    }

    [Fact]
    public void NativeWordNarrowingAndAdditionalTransitionOpcodesAreRetained()
    {
        int[] body = [Push(-1), Encode(Jsm.Opcode.SETPLACE), Push(1), Push(0), Encode(Jsm.Opcode.MAPJUMPO), Push(2), Push(0), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.DISCJUMP), Push(3), Push(0), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.MAPJUMP3)];
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, body)], []);
        Assert.Equal(ushort.MaxValue, Assert.Single(graph.Nodes[0].PlaceSetters).Value);
        Assert.Equal([1, 2, 3], graph.Nodes[0].Transitions.Select(edge => edge.Value!.Value));
        Assert.All(graph.Nodes[0].Transitions, edge => Assert.True(edge.Position.ByteOffset >= 18));
    }

    [Fact]
    public void BranchEntryIntoLiteralSequenceDoesNotProveAValue()
    {
        // JPF at operation 2 enters at MAPJUMP (operation 7), bypassing its pushes.
        int[] body = [Push(1), Encode(Jsm.Opcode.JPF, 5), Push(1), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.MAPJUMP)];
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, body), Source(1, SetPlace(0))], ["zero"]);
        Assert.Null(Assert.Single(graph.Nodes[0].Transitions).Value);
        Assert.Equal(FieldLocationStatus.NoPath, graph.Resolutions[0].Status);
        Assert.True(graph.Resolutions[0].HasDynamicOperations);
    }

    [Fact]
    public void CyclesTerminateAndMalformedScriptCannotSupplyAName()
    {
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, Jump(1)), Source(1, Jump(0)), new(2, "invalid", [1, 2, 3])], ["zero"]);
        Assert.All(graph.Resolutions, row => Assert.Equal(FieldLocationStatus.NoPath, row.Status));
        Assert.True(graph.Resolutions[2].Incomplete);
        Assert.NotEmpty(graph.Nodes[2].Problems);
    }

    [Fact]
    public void DynamicEdgesOnUnchosenRoutesPropagateUncertainty()
    {
        int[] dynamicJump = [Encode(Jsm.Opcode.PSHM_W, 100), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.MAPJUMP)];
        FieldLocationGraphResult graph = FieldLocationGraph.Build([Source(0, Jump(1).Concat(Jump(2))), Source(1, SetPlace(0)), Source(2, Jump(3)), Source(3, dynamicJump)], ["zero"]);
        Assert.Equal(FieldLocationStatus.Nearest, graph.Resolutions[0].Status);
        Assert.Equal(1, graph.Resolutions[0].Distance);
        Assert.True(graph.Resolutions[0].HasDynamicOperations);
        Assert.True(graph.Resolutions[0].Incomplete);
    }

    private static FieldLocationSource Source(int id, IEnumerable<int> body) => new(id, $"synthetic_{id}", Script(body.ToArray()));
    private static int[] Jump(int target) => [Push(target), Push(0), Push(0), Push(0), Encode(Jsm.Opcode.MAPJUMP)];
    private static int[] SetPlace(int place) => [Push(place), Encode(Jsm.Opcode.SETPLACE)];
    private static int Push(int value) => Encode(Jsm.Opcode.PSHN_L, value);
    private static int Encode(Jsm.Opcode opcode, int value = 0) => opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_W or Jsm.Opcode.JPF or Jsm.Opcode.LBL ? ((int)opcode << 24) | (value & 0xFFFFFF) : (int)opcode;

    private static byte[] Script(int[] body)
    {
        int[] operations = new[] { Encode(Jsm.Opcode.LBL) }.Concat(body).Append(Encode(Jsm.Opcode.RET)).ToArray();
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), checked((ushort)operations.Length));
        for (int index = 0; index < operations.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), operations[index]);
        }
        return bytes;
    }
}
