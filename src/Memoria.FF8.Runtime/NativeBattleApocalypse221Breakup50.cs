using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;
using M = Memoria.FF8.Runtime.NativeBattleApocalypse221BreakupMemory;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleApocalypse221Breakup50Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveNode, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, uint, uint, int, NativeBattleWaterExecutionFrame> TransformVertices, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> DrawFaces);

/// <summary>The retained triangle breakup state and its two native activation modes.</summary>
public static class NativeBattleApocalypse221Breakup50
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleApocalypse221Breakup50Services services)
    {
        M m = new(frame);
        uint payload = m.Dword(m.Slot + 0xB8);
        uint state = m.Dword(m.Slot + 0xBC);
        if (state == 0)
        {
            uint reference = services.ResolveNode(m.Frame, m.Signed(payload));
            uint shape = services.ResolveShape(m.Frame, m.Signed(payload + 2));
            int count = m.Signed(unchecked(shape + m.Dword(shape + 8) + 2));
            state = m.Allocate(unchecked((uint)(count * 36 + 20)));
            m.Dword(m.Slot + 0xBC, state);
            m.Word(state + 0xA, count);
            m.Dword(state + 0xC, shape);
            m.Word(state + 6, m.Word(payload));
            m.Word(state, m.Word(payload + 6));
            m.Word(state + 2, 0);
            m.Word(state + 4, m.Word(payload + 4));
            m.Word(state + 0x10, m.Word(reference + 0x96));
            m.Word(state + 0x12, m.Word(reference + 0x8E));
            uint vertices = m.Allocate(unchecked((uint)(count * 8)));
            m.Dword(m.Slot + 0xC0, vertices);
            uint originalVertices = unchecked(shape + m.Dword(shape + 0x14));
            m.Frame = services.TransformVertices(m.Frame, originalVertices, vertices, count);
            int duration = m.Signed(payload + 8);
            m.Dword(m.Temporary + 0xF0, unchecked((uint)M.Divide(unchecked(duration * (duration + 1) << 4), 2)));
            m.Dword(m.Temporary + 0xF4, vertices);
            m.Dword(m.Temporary + 0xF8, unchecked((uint)M.Divide(m.Signed(reference + 0x96) - 4096, duration - m.Signed(reference + 0x8E))));
            m.Dword(m.Temporary + 0x70, unchecked((uint)m.Signed(reference + 0x94)));
            m.Dword(m.Temporary + 0x74, unchecked((uint)m.Signed(reference + 0x8C)));
            uint face = unchecked(shape + m.Dword(shape + 8) + 4);
            for (int index = 0; index < count; index++, face = unchecked(face + 20))
            {
                uint record = unchecked(state + 20u + (uint)(36 * index));
                m.Dword(record, 0);
                m.Dword(record + 0x18, 0);
                m.Dword(record + 0x1C, 0);
                uint[] points = [unchecked(vertices + m.Word(face + 0xA)), unchecked(vertices + m.Word(face + 0xC)), unchecked(vertices + m.Word(face + 0xE))];
                for (uint axis = 0; axis < 3; axis++)
                {
                    m.Word(record + 4 + axis * 2, (m.Signed(points[0] + axis * 2) + m.Signed(points[1] + axis * 2) + m.Signed(points[2] + axis * 2)) / 3);
                }
                m.Word(record + 0x14, 0);
                m.Word(record + 0x16, unchecked(m.Random(m.Signed(payload + 0x10)) + m.Word(payload + 0xE)));
                int dx = m.Signed(record + 4) - m.Signed(state);
                int dz = m.Signed(record + 8) - m.Signed(state + 4);
                int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dz * dz));
                int angle = NativeWorldScriptedPathMath.NativeAngle(dx, dz) & 0xFFF;
                m.Dword(m.Temporary + 0xFC, unchecked((uint)distance));
                m.Word(record + 0x12, angle);
                distance = unchecked((short)distance);
                m.Word(record + 0xC, distance);
                int displacement = unchecked(m.Random(unchecked((short)m.Dword(m.Temporary + 0x74))) + unchecked((int)m.Dword(m.Temporary + 0x70)) - distance);
                int velocity = M.Divide(displacement, m.Signed(payload + 8)) << 1;
                m.Word(record + 0xE, velocity);
                int acceleration = M.Divide(unchecked((displacement - m.Signed(payload + 8) * unchecked((short)velocity)) << 4), unchecked((int)m.Dword(m.Temporary + 0xF0)));
                m.Word(record + 0x10, acceleration);
                short spin = m.Signed(payload + 0xC);
                m.Byte(record + 0x1B, unchecked((byte)m.Random(spin)));
                m.Byte(record + 0x1C, unchecked((byte)m.Random(spin)));
                m.Byte(record + 0x1D, unchecked((byte)m.Random(spin)));
                m.Word(record + 0x20, 4096);
                m.Word(record + 0x22, m.Word(m.Temporary + 0xF8));
            }
        }
        uint selected = services.ResolveNode(m.Frame, m.Signed(state + 6));
        m.Dword(m.Temporary + 0x88, selected);
        m.Dword(m.Temporary + 0x7C, unchecked((uint)m.Signed(payload + 8)));
        m.Dword(m.Temporary + 0x80, unchecked((uint)m.Signed(payload + 0xA)));
        m.Dword(m.Temporary + 0x90, unchecked((uint)m.Signed(state + 0x12)));
        m.Frame = Activate(m.Frame, state, m.Signed(selected + 0x98));
        return services.DrawFaces(m.Frame, state, m.Dword(m.Slot + 0xC0));
    }

    public static NativeBattleWaterExecutionFrame Activate(NativeBattleWaterExecutionFrame frame, uint state, int threshold)
    {
        M m = new(frame);
        int count = m.Signed(state + 0xA);
        for (int index = 0; index < count; index++)
        {
            uint record = unchecked(state + 20u + (uint)(36 * index));
            if (m.Word(record) == 0 && m.Signed(record + 0xC) < threshold)
            {
                m.Dword(record, 1);
            }
        }
        if ((m.Byte(m.Slot + 0x4A) & 2) == 0)
        {
            return m.Frame;
        }
        int duration = unchecked((int)m.Dword(m.Temporary + 0x80));
        m.Dword(m.Temporary + 0xF0, unchecked((uint)M.Divide(unchecked(duration * (duration + 1) << 4), 2)));
        uint reference = m.Dword(m.Temporary + 0x88);
        int angle = m.Signed(reference + 0x90);
        int firstLow = 1024 - angle;
        int firstHigh = 1024 + angle;
        int secondLow = 3072 - angle;
        int secondHigh = 3072 + angle;
        for (int index = 0; index < count; index++)
        {
            uint record = unchecked(state + 20u + (uint)(36 * index));
            ushort flags = m.Word(record);
            int phase = m.Word(record + 0x12) & 0xFFF;
            if ((flags & 2) != 0 || !(phase >= firstLow && phase <= firstHigh || phase >= secondLow && phase <= secondHigh))
            {
                continue;
            }
            m.Word(record, flags | 2);
            m.Word(record + 0xA, 0);
            firstLow = unchecked(m.Random(m.Signed(reference + 0x8C)) + m.Signed(reference + 0x94));
            firstHigh = unchecked(firstLow - m.Signed(record + 0xC));
            m.Word(record + 0xE, M.Divide(firstHigh, unchecked((int)m.Dword(m.Temporary + 0x80))));
            m.Word(record + 0x10, 0);
        }
        return m.Frame;
    }

    public static NativeBattleWaterExecutionFrame Integrate(NativeBattleWaterExecutionFrame frame, uint record)
    {
        M m = new(frame);
        if (m.Byte(m.Temporary + 0x145) != 0)
        {
            return frame;
        }
        byte flags = m.Byte(record);
        m.Word(record + 2, m.Word(record + 2) + 1);
        if ((flags & 2) != 0)
        {
            short age = m.Signed(record + 0xA);
            if (age < unchecked((int)m.Dword(m.Temporary + 0x80)))
            {
                Move();
                m.Word(record + 0xA, age + 1);
            }
        }
        else
        {
            int age = m.Signed(record + 2);
            if (age < unchecked((int)m.Dword(m.Temporary + 0x7C)))
            {
                Move();
                if (age >= unchecked((int)m.Dword(m.Temporary + 0x90)))
                {
                    m.Word(record + 0x20, m.Word(record + 0x20) + m.Word(record + 0x22));
                }
            }
        }
        m.Word(record + 0x12, m.Word(record + 0x12) + m.Word(record + 0x14));
        for (uint axis = 0; axis < 3; axis++)
        {
            m.Byte(record + 0x18 + axis, unchecked((byte)(m.Byte(record + 0x18 + axis) + m.Byte(record + 0x1B + axis))));
        }
        return m.Frame;

        void Move()
        {
            m.Word(record + 0xE, m.Word(record + 0xE) + m.Word(record + 0x10));
            m.Word(record + 0xC, m.Word(record + 0xC) + m.Word(record + 0xE));
            m.Word(record + 0x14, m.Word(record + 0x14) + m.Word(record + 0x16));
        }
    }
}
