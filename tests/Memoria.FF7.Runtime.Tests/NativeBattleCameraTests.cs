using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBattleCameraTests
{
    public static void Run()
    {
        Ff7BattleAttack attack = Ff7BattleAttack.Read(new byte[Ff7BattleAttack.RecordSize]) with { SingleTargetCamera = 12, MultipleTargetCamera = 36 };
        Check(Ff7NativeBattleCamera.SelectAction(attack, ushort.MaxValue, false, false) == new Ff7BattleActionCameras(12, 36), "Ordinary camera selection lost independent attack cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, false, false) == new Ff7BattleActionCameras(210, 210), "Enemy camera override must replace both cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, true, false) == new Ff7BattleActionCameras(12, 12), "Limit single camera must take precedence over the enemy override.");
        Check(Ff7NativeBattleCamera.SelectAction(attack, 210, true, true) == new Ff7BattleActionCameras(36, 36), "Limit multiple-target camera must replace both cameras.");
        Check(Ff7NativeBattleCamera.SelectAction(attack with { SingleTargetCamera = ushort.MaxValue }, 210, true, false) == new Ff7BattleActionCameras(ushort.MaxValue, 36), "The native camera sentinel must retain the original pair after Limit selection.");

        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP installed battle camera resources: MEMORIA_FF7_GAME_DIR is unset.");
            return;
        }

        Ff7Installation installation = Ff7Installation.Discover(root);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        foreach ((int item, ushort camera) in new (int, ushort)[] { (0, 42), (3, 44), (7, 45), (8, 42), (20, ushort.MaxValue) })
        {
            Check(Ff7NativeBattleCamera.SelectItem(kernel[4].Data.Span.Slice(item * 28, 28)) == new Ff7BattleActionCameras(camera, camera), "Original item camera must use only its one WORD at +8, including FFFF.");
        }
        Ff7ResourceRole[] roles = [Ff7ResourceRole.BattleCamera0, Ff7ResourceRole.BattleCamera1, Ff7ResourceRole.BattleCamera2];
        int[] counts = [394, 375, 374];
        for (int bank = 0; bank < roles.Length; bank++)
        {
            byte[] bytes = File.ReadAllBytes(installation.GetRequired(roles[bank]).Path);
            Ff7BattleCameraArchive archive = Ff7BattleCameraArchive.Read(bytes);
            Check(archive.CameraCount == counts[bank], "Original camera count must use DWORD entries and three variants.");
            for (int camera = 0; camera < archive.CameraCount; camera++)
            {
                foreach (Ff7BattleCameraComponent component in Enum.GetValues<Ff7BattleCameraComponent>())
                {
                    for (int track = 0; track < 3; track++)
                    {
                        ReadOnlyMemory<byte> script = archive.Script(camera, track, component);
                        int table = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)component * 4)) - 0x801A0000));
                        int offset = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(table + (camera * 3 + track) * 4)) - 0x801A0000));
                        Check(!script.IsEmpty && script.Span.SequenceEqual(bytes.AsSpan(offset, script.Length)), "A camera track changed its relocated pointer or script bytes.");
                    }
                }
            }

            for (int track = 0; track < 3; track++)
            {
                foreach (Ff7BattleCameraComponent component in Enum.GetValues<Ff7BattleCameraComponent>())
                {
                    Check(!archive.SpecialScript(track, component).IsEmpty, "The special tracks after the main tables must remain readable.");
                }
            }

            byte[] corrupt = (byte[])bytes.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt, 0x801A0000);
            Throws<InvalidDataException>(() => Ff7BattleCameraArchive.Read(corrupt));
            int focus = checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) - 0x801A0000));
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(0), BinaryPrimitives.ReadUInt32LittleEndian(bytes));
            BinaryPrimitives.WriteUInt32LittleEndian(corrupt.AsSpan(focus), 0x801A0000 + (uint)focus);
            Throws<InvalidDataException>(() => Ff7BattleCameraArchive.Read(corrupt));
            Throws<ArgumentOutOfRangeException>(() => archive.Script(archive.CameraCount, 0, Ff7BattleCameraComponent.Position));
            Throws<ArgumentOutOfRangeException>(() => archive.Script(0, 3, Ff7BattleCameraComponent.Position));
        }

        Ff7BattleCameraArchive normal = Ff7BattleCameraResources.Load(installation, 0);
        Check(normal.Script(6, 0, Ff7BattleCameraComponent.Position).Span[..8].SequenceEqual(new byte[] { 0xDC, 0xF0, 0, 0x2C, 1, 0xC3, 0xFC, 0x2E }), "Original camera 6 position operands changed.");
        Check(normal.Script(50, 1, Ff7BattleCameraComponent.Position).Span.SequenceEqual(normal.Script(50, 2, Ff7BattleCameraComponent.Position).Span), "Aliased camera track pointers changed their shared bytes.");
        Check(Ff7BattleCameraResources.Load(installation, 1).CameraCount == 394 && Ff7BattleCameraResources.Load(installation, 2).CameraCount == 375 && Ff7BattleCameraResources.Load(installation, 3).CameraCount == 374 && Ff7BattleCameraResources.Load(installation, 4).CameraCount == 374, "Native formation layout must select its original camera bank.");
        Throws<NotSupportedException>(() => Ff7BattleCameraResources.Load(installation, 5));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
