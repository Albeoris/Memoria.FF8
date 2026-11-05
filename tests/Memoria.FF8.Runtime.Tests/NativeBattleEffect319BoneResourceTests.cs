using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319BoneResourceTests
{
    [Theory]
    [InlineData(318, false, false, false)]
    [InlineData(318, false, true, false)]
    [InlineData(318, true, false, false)]
    [InlineData(318, true, true, false)]
    [InlineData(319, false, false, false)]
    [InlineData(319, false, true, false)]
    [InlineData(319, true, false, false)]
    [InlineData(319, true, true, false)]
    [InlineData(318, false, false, true)]
    [InlineData(318, true, false, true)]
    [InlineData(319, false, false, true)]
    [InlineData(319, true, false, true)]
    public void Lower_cache_restores_typed_coordinate_and_scales_current_pose_after_padding_COW(int family, bool remastered, bool logical, bool legacy)
    {
        Fixture fixture = new(family, remastered, logical, legacy);
        fixture.Birth();
        NativeBattleResourcePointer cached = fixture.Task.ReadResourcePointer(12);
        Assert.Null(cached.CpuPublication);
        if (legacy)
        {
            Assert.Empty(fixture.Task.EnumerateResourcePointers());
            Assert.Equal(fixture.Skeleton.Cpu!.PhysicalAddress + 64, cached.StagingAddress);
        }
        else
        {
            Assert.Equal(fixture.Skeleton.Cpu!.Offset + 64, cached.Cpu!.Offset);
        }
        if (logical)
        {
            Assert.Throws<NotSupportedException>(() => fixture.Task.ReadDword(12));
        }
        else
        {
            Assert.Equal(fixture.Skeleton.Cpu.PhysicalAddress + 64, fixture.Task.ReadDword(12));
        }
        fixture.RestoreFrame();
        Assert.Equal(cached, fixture.Task.ReadResourcePointer(12));
        fixture.PaddingCOW();
        NativeBattleActionMatrix original = fixture.Graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[1];
        fixture.Advance();
        EqualMatrix(NativeBattlePoseMatrices.Scale56BEF0(original, 2048, 2048, 1024), fixture.Graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[1]);
        Assert.Equal(original, fixture.Graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[0]);
        NativeBattleActionMatrix refreshed = original with { X = -700000, Y = 800000, Z = -900000, Rotation = [3000, 2000, 1000, 6000, 5000, 4000, 9000, 8000, 7000] };
        NativeBattleGraphicsModel model = fixture.Graphics.Models[0];
        fixture.Graphics = fixture.Graphics with { Models = fixture.Graphics.Models.SetItem(0, model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [original, refreshed] } }) };
        fixture.RestoreFrame();
        fixture.Advance();
        EqualMatrix(NativeBattlePoseMatrices.Scale56BEF0(refreshed, 4096, 4096, 2048), fixture.Graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[1]);
        Assert.Equal(original, fixture.Graphics.Models[0].CurrentBodyBoneMatrices!.Matrices[0]);
        Assert.Equal(cached, fixture.Task.ReadResourcePointer(12));
    }

    [Theory]
    [InlineData(318, 0)]
    [InlineData(318, 1)]
    [InlineData(318, 2)]
    [InlineData(318, 3)]
    [InlineData(318, 4)]
    [InlineData(318, 5)]
    [InlineData(318, 6)]
    [InlineData(319, 0)]
    [InlineData(319, 1)]
    [InlineData(319, 2)]
    [InlineData(319, 3)]
    [InlineData(319, 4)]
    [InlineData(319, 5)]
    [InlineData(319, 6)]
    public void Production_scaling_rejects_cross_owner_domain_layout_misaligned_outside_and_immutable_cache_records(int family, int corruption)
    {
        Fixture fixture = new(family, false, true);
        NativeBattleResourcePointer record = fixture.Resolve(1);
        record = corruption switch
        {
            0 => record with { Cpu = record.Cpu! with { Bank = 1 } },
            1 => record with { Cpu = record.Cpu! with { ResourceOnly = false, Offset = 64 } },
            2 => record with { Cpu = record.Cpu! with { Layout = NativeBattlePartyCpuLayout.Remastered } },
            3 => record with { Cpu = record.Cpu! with { Offset = record.Cpu.Offset + 1 } },
            4 => record with { Cpu = record.Cpu! with { Offset = record.Cpu.Offset + 48 } },
            5 => record with { CpuPublication = fixture.Cpu.Cpu.Banks[0] },
            6 => record with { Cpu = null, StagingAddress = 0x1000 },
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        string before = NativeBattleTestSnapshots.Json(fixture.Graphics.Models[0]);
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(() => fixture.Execute(record, 2048, 1024));
        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(fixture.Graphics.Models[0]));
    }

    [Fact]
    public void Record_coordinates_preserve_shared_domain_and_strip_asset_pins_without_freezing_mutable_pose()
    {
        NativeBattleResourcePointer shared = new(null, 0x1000, 112);
        NativeBattleResourcePointer record = NativeBattleEffect319BoneCache.ResolveRecord(shared, 2, 1);
        Assert.Equal(0x1040u, record.StagingAddress);
        Assert.Equal(1, NativeBattleEffect319BoneCache.ResolveIndex(shared, 2, record));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveIndex(shared, 1, record));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveIndex(shared, 2, shared));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveRecord(shared, 2, -1));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveRecord(shared with { Length = 111 }, 2, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveIndex(shared, 2, record with { Length = 47 }));
        NativeBattleResourcePointer pinned = new(new(0, 4), null, 112) { CpuPublication = NativeBattleModelCpuBanks.Unknown().Banks[0] };
        Assert.Null(NativeBattleEffect319BoneCache.ResolveRecord(pinned, 2, 1).CpuPublication);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveRecord(new NativeBattleResourcePointer(null, null) { CpuRow = 0 }, 2, 1));
    }

    private static void EqualMatrix(NativeBattleActionMatrix expected, NativeBattleActionMatrix actual)
    {
        Assert.Equal(expected.Rotation, actual.Rotation);
        Assert.Equal((expected.X, expected.Y, expected.Z, expected.Scale), (actual.X, actual.Y, actual.Z, actual.Scale));
    }

    private sealed class Fixture
    {
        private readonly int family;
        private readonly bool legacy;
        private readonly BattleSession session;
        private readonly NativeBattleEffect318LowerTests.Fixture? effect318;
        private readonly NativeBattleEffect319LowerTests.Fixture? effect319;
        private object frame;
        internal NativeBattleGraphicsState Graphics;
        internal NativeBattleModelCpuResources Cpu;
        internal NativeBattleResourcePointer Skeleton;
        internal NativeBattleModulePoolTask Task => family == 318 ? NativeBattleEffect318Controller.Read((NativeBattleEffect318Frame)frame, effect318!.Address) : NativeBattleEffect319Controller.Read((NativeBattleEffect319Frame)frame, effect319!.Address);

        internal Fixture(int family, bool remastered, bool logical, bool legacy = false)
        {
            this.family = family;
            this.legacy = legacy;
            if (family == 318)
            {
                effect318 = new(0x65F2B0, false);
                frame = effect318.Frame with { State = effect318.Frame.State.WriteDword(NativeBattleEffect318Profile.CasterGlobal, 0x1D972C0) };
            }
            else
            {
                effect319 = new(0x65F2B0, false);
                frame = effect319.Frame with { State = effect319.Frame.State.WriteDword(NativeBattleEffect319Profile.CasterGlobal, 0x1D972C0) };
            }
            byte[] skeleton = new byte[112];
            skeleton[0] = 2;
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
            NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
            NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
            int offset = logical ? layout.BankLength + 4 : 4;
            Cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
            NativeBattleModelCpuBank bank = Cpu.Cpu.Banks[0].BindPartyResource(offset, source.Skeleton, 0, skeleton.Length);
            Skeleton = new(new(0, offset) { ResourceOnly = logical, Layout = layout }, null, skeleton.Length);
            NativeBattleModelCpuResourceRow row = Cpu.Rows[0].Set(12, Skeleton.Cpu) with { Lengths = ImmutableDictionary<int, int>.Empty.Add(12, skeleton.Length) };
            Cpu = Cpu with { Cpu = Cpu.Cpu with { Banks = Cpu.Cpu.Banks.SetItem(0, bank) }, Rows = Cpu.Rows.SetItem(0, row) };
            session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
            BattleDefinition definition = (BattleDefinition)typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            NativeBattleGraphicsResource resource = new([0, 0]) { PoseSource = source };
            typeof(BattleDefinition).GetProperty(nameof(BattleDefinition.NativeGraphics))!.SetValue(definition, new NativeBattleGraphicsDefinition(null!, [new("party", 3, resource)], []));
            SetCpu();
            NativeBattleActionMatrix matrix = new([1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000], 123456, -234567, 345678, 4096);
            NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 3, ActiveResourceRow100 = 0, CurrentBodyBoneMatrices = new(NativeBattleBodyBoneMatrices.Identity(source), [matrix, matrix]) };
            Graphics = new(NativeBattleGraphicsModels.ClearBank().ToImmutableArray().SetItem(0, model), ["party", null, null, "enemy", null, null, null], 0, 0);
        }

        internal NativeBattleResourcePointer Resolve(int bone)
        {
            return (NativeBattleResourcePointer)typeof(BattleSession).GetMethod($"ResolveNativeEffect{family}BoneRecord", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, [Graphics, frame, 0x1D972C0u, bone])!;
        }

        internal void Birth()
        {
            if (family == 318)
            {
                NativeBattleEffect318Inputs inputs = effect318!.Inputs() with { ResolveBoneRecord = (_, _, bone) => Resolve(bone).Cpu!.PhysicalAddress, ResolveBoneRecordReference = legacy ? null : (_, _, bone) => Resolve(bone) };
                frame = NativeBattleEffect318Lower.Birth65F250((NativeBattleEffect318Frame)frame, 1, 1, 1024, 2048, inputs);
            }
            else
            {
                NativeBattleEffect319Inputs inputs = effect319!.Inputs() with { ResolveBoneRecord = (_, _, bone) => Resolve(bone).Cpu!.PhysicalAddress, ResolveBoneRecordReference = legacy ? null : (_, _, bone) => Resolve(bone) };
                frame = NativeBattleEffect319Lower.Birth65E4B0((NativeBattleEffect319Frame)frame, 1, 1, 1024, 2048, inputs);
            }
        }

        internal void Advance()
        {
            if (family == 318)
            {
                NativeBattleEffect318Inputs inputs = effect318!.Inputs() with { Execute = (current, operation) => (NativeBattleEffect318Frame)ExecuteOperation(current, operation) };
                NativeBattleEffect318Step step = NativeBattleEffect318Lower.Advance((NativeBattleEffect318Frame)frame, effect318.Address, effect318.Resources, inputs);
                Assert.Equal(0, step.Result);
                frame = step.Frame;
            }
            else
            {
                NativeBattleEffect319Inputs inputs = effect319!.Inputs() with { Execute = (current, operation) => (NativeBattleEffect319Frame)ExecuteOperation(current, operation) };
                NativeBattleEffect319Step step = NativeBattleEffect319Lower.Advance((NativeBattleEffect319Frame)frame, effect319.Address, effect319.Resources, inputs);
                Assert.Equal(0, step.Result);
                frame = step.Frame;
            }
        }

        internal void Execute(NativeBattleResourcePointer record, short xy, short z)
        {
            object operation = family == 318 ? (object)new NativeBattleEffect318ScaleBoneReference56BEF0(record, xy, z) : new NativeBattleEffect319ScaleBoneReference56BEF0(record, xy, z);
            frame = ExecuteOperation(frame, operation);
        }

        private object ExecuteOperation(object current, object operation)
        {
            object?[] arguments = [Graphics, current, operation, null];
            object result = typeof(BattleSession).GetMethod($"ExecuteNativeEffect{family}Operation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(session, arguments)!;
            Graphics = (NativeBattleGraphicsState)arguments[0]!;
            return result;
        }

        internal void PaddingCOW()
        {
            NativeBattleFileStaging staging = family == 318 ? ((NativeBattleEffect318Frame)frame).Staging! : ((NativeBattleEffect319Frame)frame).Staging!;
            NativeBattleModelCpuBank old = Cpu.Cpu.Banks[0];
            Cpu = NativeBattleModelResourceView.WritePartySection(Cpu, staging, Skeleton, 50, [1, 0]);
            Assert.NotSame(old, Cpu.Cpu.Banks[0]);
            SetCpu();
        }

        private void SetCpu() => typeof(BattleSession).GetProperty(nameof(BattleSession.NativeModelCpuResources))!.SetValue(session, Cpu);

        internal void RestoreFrame()
        {
            frame = family == 318 ? NativeBattleTestSnapshots.RestoreJson<NativeBattleEffect318Frame>(NativeBattleTestSnapshots.Json((NativeBattleEffect318Frame)frame)) : NativeBattleTestSnapshots.RestoreJson<NativeBattleEffect319Frame>(NativeBattleTestSnapshots.Json((NativeBattleEffect319Frame)frame));
        }
    }
}
