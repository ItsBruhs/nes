public class Cpu(Memory memory)
{
    // Registers
    public byte A, X, Y, SP, P;

    // Flags
    public bool C, Z, I, D, B, V, N;

    // Program counter
    public ushort PC;

    public Memory Memory = memory;

    public void Reset(ushort pc)
    {
        PC = pc;
        SP = 0xFD;
        P = 0x24;
    }

    private void Push(byte value)
    {
        Memory.Write(SP, value);
        SP -= 1;
    }

    private void Push(ushort value)
    {
        byte low = (byte)(value & 0xFF);
        byte high = (byte)((value >> 8) & 0xFF);

        Memory.Write(SP, low);
        Memory.Write(SP, high);
        SP -= 2;
    }

    private byte PopByte()
    {
        var value = Memory.Read(SP);
        SP += 1;
        return value;
    }

    private ushort PopUshort()
    {
        var low = Memory.Read(SP);
        var high = Memory.Read(SP);
        SP += 2;

        ushort value = (ushort)(low | (high << 8));
        return value;
    }

    public void Step()
    {
        var opcode = Memory.Read(PC++);

        switch (opcode)
        {
            case 0x4c:
                var jmpLow = (ushort)Memory.Read(PC++);
                var jmpHigh = (ushort)Memory.Read(PC++);
                ushort jmpTarget = (ushort)(jmpLow | (jmpHigh << 8));
                LogInstruction(opcode, $"JMP ${jmpTarget:X4}", jmpLow, jmpHigh);
                PC = jmpTarget;
                break;

            case 0xa2:
                var ldxAddress = (ushort)Memory.Read(PC++);
                X = Memory.Read(ldxAddress);
                Z = X == 0;
                N = (X & (1 << 7 - 1)) != 0;
                LogInstruction(opcode, $"LDX #${ldxAddress:X2}", ldxAddress);
                break;

            case 0x86:
                var stxAddress = (ushort)Memory.Read(PC++);
                Memory.Write(stxAddress, X);
                LogInstruction(opcode, $"STX ${stxAddress:X2} = {X:X2}", stxAddress);
                break;

            case 0x20:
                var jsrLow = (ushort)Memory.Read(PC++);
                var jsrHigh = (ushort)Memory.Read(PC++);
                ushort jsrTarget = (ushort)(jsrLow | (jsrHigh << 8));
                LogInstruction(opcode, $"JSR ${jsrTarget:X4}", jsrLow, jsrHigh);
                Push((ushort)(PC - 1));
                PC = jsrTarget;
                break;

            case 0xea:
                LogInstruction(opcode, $"NOP");
                break;

            case 0x38:
                LogInstruction(opcode, $"SEC");
                C = true;
                break;

            default:
                throw new NotImplementedException($"Unimplemented opcode at {PC:X}: {opcode:X}");
        }
    }

    public void LogInstruction(ushort opcode, string instruction, params ushort[] operands)
    {
        // ABCD  AB CD EF  OP $ABCD                        [registers]
        Console.WriteLine($"{PC,-6:X}{$"{opcode:X2} {string.Join(" ", operands.Select(op => op.ToString("X2")))}",-10}{instruction,-32}{GetRegisters()}");
    }

    public string GetRegisters() => $"A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{SP:X2}";
}
