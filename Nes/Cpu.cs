namespace Nes;

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
        UpdateFlagsFromP();
    }

    private void Push(byte value)
    {
        Memory.Write((ushort)(0x0100 + SP), value);
        SP--;
    }

    private void Push(ushort value)
    {
        Push((byte)((value >> 8) & 0xFF)); // high byte
        Push((byte)(value & 0xFF)); // low byte
    }

    private byte PopByte()
    {
        SP++;
        return Memory.Read((ushort)(0x0100 + SP));
    }

    private ushort PopUshort()
    {
        byte low = PopByte();
        byte high = PopByte();
        return (ushort)((high << 8) | low);
    }

    public void Step()
    {
        ushort currentPC = PC;
        var opcode = Memory.Read(PC++);

        switch (opcode)
        {
            case 0x4c:
                var jmpLow = (ushort)Memory.Read(PC++);
                var jmpHigh = (ushort)Memory.Read(PC++);
                ushort jmpTarget = (ushort)(jmpLow | (jmpHigh << 8));

                LogInstruction(currentPC, opcode, $"JMP ${jmpTarget:X4}", jmpLow, jmpHigh);

                PC = jmpTarget;
                break;

            case 0xa2:
                var ldxValue = Memory.Read(PC++);

                LogInstruction(currentPC, opcode, $"LDX #${ldxValue:X2}", ldxValue);

                X = ldxValue;
                Z = X == 0;
                N = (X & (1 << 7 - 1)) != 0;
                break;

            case 0x86:
                var stxAddress = (ushort)Memory.Read(PC++);

                LogInstruction(currentPC, opcode, $"STX ${stxAddress:X2} = {X:X2}", stxAddress);

                Memory.Write(stxAddress, X);
                break;

            case 0x20:
                var jsrLow = (ushort)Memory.Read(PC++);
                var jsrHigh = (ushort)Memory.Read(PC++);
                ushort jsrTarget = (ushort)(jsrLow | (jsrHigh << 8));

                LogInstruction(currentPC, opcode, $"JSR ${jsrTarget:X4}", jsrLow, jsrHigh);

                Push(PC);
                PC = jsrTarget;
                break;

            case 0xea:
                LogInstruction(currentPC, opcode, $"NOP");
                break;

            case 0x38:
                LogInstruction(currentPC, opcode, $"SEC");

                C = true;
                break;

            case 0xb0:
                var bcsOffset = (ushort)Memory.Read(PC++);
                var bcsTarget = (ushort)(PC + bcsOffset);

                LogInstruction(currentPC, opcode, $"BCS ${bcsTarget:X4}", bcsOffset);

                if (C)
                    PC = bcsTarget;
                break;

            case 0x18:
                LogInstruction(currentPC, opcode, $"CLC");

                C = false;
                break;

            case 0x90:
                var bccOffset = (ushort)Memory.Read(PC++);
                var bccTarget = (ushort)(PC + bccOffset);

                LogInstruction(currentPC, opcode, $"BCC ${bccTarget:X4}", bccOffset);

                if (!C)
                    PC = bccTarget;
                break;

            case 0xa9:
                var ldaValue = Memory.Read(PC++);

                LogInstruction(currentPC, opcode, $"LDA #${ldaValue:X2}", ldaValue);

                A = ldaValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                break;

            case 0xf0:
                var beqOffset = (ushort)Memory.Read(PC++);
                var beqTarget = (ushort)(PC + beqOffset);

                LogInstruction(currentPC, opcode, $"BEQ ${beqTarget:X4}", beqOffset);

                if (Z)
                    PC = beqTarget;
                break;

            case 0xd0:
                var bneOffset = (ushort)Memory.Read(PC++);
                var bneTarget = (ushort)(PC + bneOffset);

                LogInstruction(currentPC, opcode, $"BNE ${bneTarget:X4}", bneOffset);

                if (!Z)
                    PC = bneTarget;
                break;

            case 0x85:
                var staAddress = (ushort)Memory.Read(PC++);
                var staValueAtAddress = Memory.Read(staAddress);

                LogInstruction(currentPC, opcode, $"STA ${staAddress:X2} = {staValueAtAddress:X2}", staAddress);

                Memory.Write(staAddress, A);
                break;

            case 0x24:
                var bitAddress = Memory.Read(PC++);
                var bitValue = Memory.Read(bitAddress);

                LogInstruction(currentPC, opcode, $"BIT ${bitAddress:X2} = {A:X2}", bitAddress);

                Z = (A & bitValue) == 0;
                N = (bitValue & (1 << 7 - 1)) != 0;
                V = (bitValue & (1 << 6 - 1)) != 0;
                break;

            case 0x70:
                var bvsOffset = (ushort)Memory.Read(PC++);
                var bvsTarget = (ushort)(PC + bvsOffset);

                LogInstruction(currentPC, opcode, $"BVS ${bvsTarget:X4}", bvsOffset);

                if (V)
                    PC = bvsTarget;
                break;

            case 0x50:
                var bvcOffset = (ushort)Memory.Read(PC++);
                var bvcTarget = (ushort)(PC + bvcOffset);

                LogInstruction(currentPC, opcode, $"BVC ${bvcTarget:X4}", bvcOffset);

                if (!V)
                    PC = bvcTarget;
                break;

            case 0x10:
                var bplOffset = (ushort)Memory.Read(PC++);
                var bplTarget = (ushort)(PC + bplOffset);

                LogInstruction(currentPC, opcode, $"BPL ${bplTarget:X4}", bplOffset);

                if (!N)
                    PC = bplTarget;
                break;

            case 0x60:
                LogInstruction(currentPC, opcode, $"RTS");

                var rtsTarget = PopUshort();
                PC = rtsTarget;
                break;

            case 0x78:
                LogInstruction(currentPC, opcode, $"SEI");

                I = true;
                break;

            case 0xf8:
                LogInstruction(currentPC, opcode, $"SED");

                D = true;
                break;

            case 0x08:
                LogInstruction(currentPC, opcode, $"PHP");

                byte flags = (byte)(P | 0b0011_0000);
                Push(flags);
                break;

            case 0x68:
                LogInstruction(currentPC, opcode, $"PLA");

                var plaValue = PopByte();
                A = plaValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                break;

            case 0x29:
                var andValue = Memory.Read(PC++);

                LogInstruction(currentPC, opcode, $"AND #${andValue:X2}", andValue);

                A &= andValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                break;

            default:
                throw new NotImplementedException($"Unimplemented opcode at {currentPC:X}: {opcode:X}, registers: {GetRegisters()}");
        }
    }

    private void UpdatePFromFlags()
    {
        P = 0;
        if (N) P |= 1 << 7;
        if (V) P |= 1 << 6;
        P |= 1 << 5; // Unused bit, always set
        if (B) P |= 1 << 4;
        if (D) P |= 1 << 3;
        if (I) P |= 1 << 2;
        if (Z) P |= 1 << 1;
        if (C) P |= 1 << 0;
    }

    private void UpdateFlagsFromP()
    {
        N = (P & (1 << 7)) != 0;
        V = (P & (1 << 6)) != 0;
        // bit 5 is ignored
        B = (P & (1 << 4)) != 0;
        D = (P & (1 << 3)) != 0;
        I = (P & (1 << 2)) != 0;
        Z = (P & (1 << 1)) != 0;
        C = (P & (1 << 0)) != 0;
    }


    public void LogInstruction(ushort pc, ushort opcode, string instruction, params ushort[] operands)
    {
        UpdatePFromFlags();

        // ABCD  AB CD EF  OP $ABCD                        [registers]
        Console.WriteLine($"{pc,-6:X}{$"{opcode:X2} {string.Join(" ", operands.Select(op => op.ToString("X2")))}",-10}{instruction,-32}{GetRegisters()}");
    }

    public string GetRegisters() => $"A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{SP:X2}";
}
