using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMoogle96NodeContractsTests
{
    [Fact]
    public void Mode2UsesExplicitDogVectorsAndSignedPhysicalWord60MaximumY()
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x64, 1), NativeBattleEffectTaskPool.Initialize(0x3000, 0x4000, 0x2A4, 1)]);
        NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, 1, 0x64);
        hierarchy = root.Hierarchy.Write(root.Address, root.Hierarchy.Read(root.Address).WriteWord(0x5A, 1));
        NativeBattleEffectHierarchyAppend child = hierarchy.Append8DC540(1, NativeBattleMoogle96Nodes.Callback, 0x2A4, root.Address, (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = child.Hierarchy.Read(child.Address).WriteWord(0x29C, 2).WriteWord(0x29E, 45);
        NativeBattleActionPacketsState packets = NativeBattleRetainedMemory.FromColdProcess().ActionPackets;
        (packets, _) = packets.Prepare(new(0, 240, 0, 0, 1, 96, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleGraphicsModel Model(int actor) => new(actor, 3) { Transform = new(new(0, 0, 0), new((short)(actor == 0 ? 100 : 200), 0, 0), 0, (short)(actor == 0 ? 111 : 222), 4096), GeometryBounds52 = new(new(0, 0, 0), new((short)(actor == 0 ? 333 : 444), (short)(actor == 0 ? -555 : -666), (short)(actor == 0 ? 777 : 888))) };
        NativeBattlePoseSource Pose(int actor) => throw new InvalidOperationException("Mode2 must not substitute an actor bone read for supplied Dog vectors.");
        NativeBattleEffectNodeDogOrigins dog = new(new(-10, -20, -30), new(40, -50, 60));
        NativeBattleEffectParameters parameters = new(0, 1, 8, 0, 0, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectNodeContext.Initialize(task, child.Hierarchy.Read(root.Address), parameters, packets, Model, Pose, 0x169227C));
        NativeBattleModulePoolTask actual = NativeBattleEffectNodeContext.Initialize(task, child.Hierarchy.Read(root.Address), parameters, packets, Model, Pose, 0x169227C, dogOrigins: dog);
        Vector(0x1C4, new(-10, 111, -30));
        Vector(0x1D4, new(-10, 0, -30));
        Vector(0x1E4, dog.ActorOrigin);
        Vector(0x1F4, dog.ActorOrigin);
        Vector(0x204, new(-10, -555, -30));
        Vector(0x54, new(40, 222, 60));
        Vector(0x94, new(40, 0, 60));
        Vector(0xD4, dog.TargetOrigin);
        Vector(0x114, dog.TargetOrigin);
        Vector(0x154, new(40, -666, 60));
        Assert.Equal(100u, actual.ReadDword(0x34));
        void Vector(int relative, NativeBattleVector expected)
        {
            Assert.Equal(unchecked((uint)(expected.X << 16)), actual.ReadDword(0x34 + relative));
            Assert.Equal(unchecked((uint)(expected.Y << 16)), actual.ReadDword(0x34 + relative + 4));
            Assert.Equal(unchecked((uint)(expected.Z << 16)), actual.ReadDword(0x34 + relative + 8));
        }
    }

    [Fact]
    public void FullLifeFt3PreservesMaterialWritesAndNearRejectionWithoutAWindowBracket()
    {
        byte[] face = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(face, 0x26808080);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 0x0605);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0xABCD0403);
        NativeBattleModuleProjectedVertex[] corners = [new(XY(100, 100), 4096, 0), new(XY(300, 100), 4096, 0), new(XY(100, 300), 4096, 0)];
        NativeBattleFullLifeMeshContext context = new(0x30, null, null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch batch = NativeBattleRestorationMeshPackets.EmitFt3_884940(face, index => corners[index], context);
        Assert.Equal(32, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt accepted = Assert.Single(batch.Attempts);
        Assert.NotNull(accepted.Primitive);
        Assert.Equal(0x12340201u, BinaryPrimitives.ReadUInt32LittleEndian(accepted.Data.AsSpan().Slice(12)));
        Assert.Equal(0xABCD0403u, BinaryPrimitives.ReadUInt32LittleEndian(accepted.Data.AsSpan().Slice(20)));
        Assert.Equal((byte)0x26, (byte)(accepted.Primitive!.Words[1] >> 24));
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryBatch rejected = NativeBattleRestorationMeshPackets.EmitFt3_884940(face, index => corners[index], context);
        Assert.Equal(0, rejected.AllocationBytes);
        Assert.Null(Assert.Single(rejected.Attempts).Primitive);
        Assert.All(rejected.Attempts[0].WriteMask.Skip(8).Take(4), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }
    private static uint XY(short x, short y) => unchecked((uint)(ushort)x | ((uint)(ushort)y << 16));
}
