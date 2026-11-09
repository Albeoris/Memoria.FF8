using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

internal static class InstalledAudit
{
    public static int Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure MEMORIA_FF7_GAME_DIR for the optional original-resource audit.");
        var installation = Ff7Installation.Discover(root);
        using var archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.FieldArchive).Path);
        foreach (string name in new[] { "startmap", "md1stin" })
        {
            var entry = archive.Find(name).Single();
            var field = PcFieldFile.ReadCompressed(archive.ReadEntry(entry));
            var triggers = PcFieldTriggers.Read(field.GetSection(PcFieldSectionKind.TriggersAndGateways).Span);
            Console.WriteLine($"INF active BG triggers {triggers.Triggers.Count(trigger => trigger.BackgroundParameter != 255)}, gateways {triggers.Gateways.Count(gateway => gateway.DestinationField != 0x7FFF)}");
            var scripts = PcFieldScripts.Read(field.GetSection(PcFieldSectionKind.ScriptAndText).Span);
            var host = new AuditHost();
            var machine = new FieldMachine(FieldProgram.FromPcScripts(scripts), host: host);
            Console.WriteLine($"FIELD {name}: entities {scripts.Entities.Count}, texts {scripts.Texts.Count}, scale {scripts.Scale}");
            try
            {
                machine.Initialize();
                Console.WriteLine("INITIALIZED");
                for (int tick = 0; tick < 1000; tick++)
                {
                    machine.AdvanceTick();
                }
                Console.WriteLine("1000 TICKS COMPLETE (diagnostic host only)");
            }
            catch (FieldExecutionException error)
            {
                var context = error.Context;
                Console.WriteLine($"BOUNDARY entity {context.Entity} ({scripts.Entities[context.Entity].Name}) PC {context.ProgramCounter:X4} op {context.Opcode:X2}: {error.InnerException?.Message ?? error.Message}");
                int length = Math.Min(16, scripts.Data.Length - context.ProgramCounter);
                if (length > 0)
                {
                    Console.WriteLine("OPERANDS " + Convert.ToHexString(scripts.Data.Span.Slice(context.ProgramCounter, length)));
                }
            }
            Console.WriteLine("HOST COUNTS " + string.Join(", ", host.Counts.Select(pair => $"{pair.Key}:{pair.Value}")));
        }
        return 0;
    }

    private sealed class AuditHost : IFieldHost
    {
        public SortedDictionary<string, int> Counts { get; } = [];
        public FieldHostReply Execute(FieldHostRequest request)
        {
            if (request.Command is FieldBattleCommand)
            {
                throw new NotSupportedException("Diagnostic audit stops at battle; no rewards or victory fabricated.");
            }
            if (request.Command is FieldFadeCommand fade)
            {
                Console.WriteLine($"FADE {fade}");
            }
            string name = request.Command.GetType().Name;
            Counts[name] = Counts.GetValueOrDefault(name) + 1;
            return new FieldHostReply(true);
        }
    }
}
