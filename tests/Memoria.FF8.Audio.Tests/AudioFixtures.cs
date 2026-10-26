using System.Text;
using Memoria.FF8.Audio;

namespace Memoria.FF8.Tests;

internal static class AudioFixtures
{
    public static DlsBank Bank(int velocityLow = 1, int velocityHigh = 127, bool duplicateLocale = false, bool drum = false)
    {
        byte[] format = Data(w =>
        {
            w.Write((ushort)1);
            w.Write((ushort)1);
            w.Write(44100);
            w.Write(88200);
            w.Write((ushort)2);
            w.Write((ushort)16);
        });
        byte[] samples = Data(w =>
        {
            for (int i = 0; i < 32; i++)
            {
                w.Write((short)8000);
            }
        });
        byte[] settings = Data(w =>
        {
            w.Write(20);
            w.Write((ushort)60);
            w.Write((short)0);
            w.Write(0);
            w.Write(0);
            w.Write(1);
            w.Write(16);
            w.Write(0);
            w.Write(0);
            w.Write(32);
        });
        byte[] wave = Form("LIST", "wave", Chunk("fmt ", format), Chunk("wsmp", settings), Chunk("data", samples));
        byte[] region = Form("LIST", "rgn ", Chunk("rgnh", Data(w =>
        {
            w.Write((ushort)0);
            w.Write((ushort)127);
            w.Write((ushort)velocityLow);
            w.Write((ushort)velocityHigh);
            w.Write((ushort)0);
            w.Write((ushort)0);
        })), Chunk("wlnk", new byte[12]));
        byte[] art = Data(w =>
        {
            w.Write(8);
            w.Write(1);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)0x209);
            w.Write((ushort)0);
            w.Write(-12000 * 65536);
        });
        byte[] instrument = Form("LIST", "ins ", Chunk("insh", Data(w =>
        {
            w.Write(1);
            w.Write(drum ? 0x80000000u : 0);
            w.Write(0);
        })), Form("LIST", "lrgn", region), Form("LIST", "lart", Chunk("art1", art)));
        byte[] duplicateRegion = Form("LIST", "rgn ", Chunk("rgnh", Data(w =>
        {
            w.Write((ushort)0);
            w.Write((ushort)1);
            w.Write((ushort)1);
            w.Write((ushort)127);
            w.Write((ushort)0);
            w.Write((ushort)0);
        })), Chunk("wlnk", new byte[12]));
        byte[] duplicateInstrument = Form("LIST", "ins ", Chunk("insh", Data(w =>
        {
            w.Write(1);
            w.Write(0);
            w.Write(0);
        })), Form("LIST", "lrgn", duplicateRegion));
        byte[] instruments = duplicateLocale ? Form("LIST", "lins", instrument, duplicateInstrument) : Form("LIST", "lins", instrument);
        return new DlsBank(Form("RIFF", "DLS ", Chunk("colh", Data(w => w.Write(duplicateLocale ? 2 : 1))), instruments, Chunk("ptbl", Data(w =>
        {
            w.Write(8);
            w.Write(1);
            w.Write(0);
        })), Form("LIST", "wvpl", wave)));

    }

    public static MusicSegment Segment(uint repeats = 0, int loopStart = 0, int loopEnd = 768, int length = 768, int duration = 768, int channel = 0)
    {
        byte[] header = Data(w =>
        {
            w.Write(repeats);
            w.Write(length);
            w.Write(0);
            w.Write(loopStart);
            w.Write(loopEnd);
            w.Write(0);
        });
        byte[] tempo = Data(w =>
        {
            w.Write(16);
            w.Write(0);
            w.Write(0);
            w.Write(120d);
        });
        byte[] events = Data(w =>
        {
            w.Write(20);
            w.Write(0);
            w.Write(duration);
            w.Write(channel);
            w.Write((short)0);
            w.Write((byte)0x90);
            w.Write((byte)60);
            w.Write((byte)127);
            w.Write(new byte[3]);
        });
        return new MusicSegment(Form("RIFF", "DMSG", Chunk("segh", header), Form("LIST", "trkl", Track("tetr", Chunk("tetr", tempo)), Track("seqt", Chunk("seqt", Chunk("evtl", events))))));

    }

    public static MusicSegment SystemSegment(byte[] message)
    {
        byte[] header = Data(w =>
        {
            w.Write(0);
            w.Write(768);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
        });
        byte[] system = Data(w =>
        {
            w.Write(0);
            w.Write(0);
            w.Write(message.Length);
            w.Write(message);
        });
        return new MusicSegment(Form("RIFF", "DMSG", Chunk("segh", header), Form("LIST", "trkl", Track("syex", Chunk("syex", system)))));
    }

    private static byte[] Track(string identifier, byte[] payload)
    {
        int suffix = identifier switch
        {
            "tetr" => 0x85,
            "seqt" => 0x86,
            "syex" => 0x87,
            _ => throw new ArgumentException(nameof(identifier))
        };
        byte[] header = Data(w =>
        {
            w.Write(new Guid($"d2ac28{suffix:x2}-b39b-11d1-8704-00600893b1bd").ToByteArray());
            w.Write(0);
            w.Write(1);
            w.Write(Encoding.ASCII.GetBytes(identifier));
            w.Write(0);
        });
        return Form("RIFF", "DMTK", Chunk("trkh", header), payload);
    }

    public static byte[] Form(string id, string type, params byte[][] children)
    {
        return Chunk(id, Encoding.ASCII.GetBytes(type).Concat(children.SelectMany(c => c)).ToArray());

    }

    public static byte[] Chunk(string id, byte[] bytes)
    {
        return Data(w =>
        {
            w.Write(Encoding.ASCII.GetBytes(id));
            w.Write(bytes.Length);
            w.Write(bytes);
            if ((bytes.Length & 1) != 0)
            {
                w.Write((byte)0);
            }
        });

    }

    private static byte[] Data(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        return stream.ToArray();

    }
}
