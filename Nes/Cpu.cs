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
        Push((byte)(value & 0xFF)); // low byte
        Push((byte)((value >> 8) & 0xFF)); // high byte
    }

    private byte PopByte()
    {
        SP++;
        return Memory.Read((ushort)(0x0100 + SP));
    }

    private ushort PopUshort()
    {
        var high = PopByte();
        var low = PopByte();
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
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xa0:
                var ldyValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"LDY #${ldyValue:X2}", ldyValue);

                Y = ldyValue;
                Z = Y == 0;
                N = (Y & (1 << 7)) != 0;
                UpdatePFromFlags();
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
                UpdatePFromFlags();
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
                UpdatePFromFlags();
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
                UpdatePFromFlags();
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
                LogInstruction(currentPC, opcode, $"BIT ${bitAddress:X2} = {bitValue:X2}", bitAddress);

                Z = (A & bitValue) == 0;
                N = (bitValue & (1 << 7)) != 0;
                V = (bitValue & (1 << 6)) != 0;
                UpdatePFromFlags();
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

                var rtsTarget = (ushort)(PopUshort() + 1);
                PC = rtsTarget;
                break;

            case 0x78:
                LogInstruction(currentPC, opcode, $"SEI");
                I = true;
                UpdatePFromFlags();
                break;

            case 0xf8:
                LogInstruction(currentPC, opcode, $"SED");
                D = true;
                UpdatePFromFlags();
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
                UpdatePFromFlags();
                break;

            case 0x29:
                var andValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"AND #${andValue:X2}", andValue);

                A &= andValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xc9:
                var cmpValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CMP #${cmpValue:X2}", cmpValue);

                var cmpResult = (byte)(A - cmpValue);
                Z = A == cmpValue;
                C = A >= cmpValue;
                N = (cmpResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xd8:
                LogInstruction(currentPC, opcode, $"CLD");
                D = false;
                UpdatePFromFlags();
                break;

            case 0x48:
                LogInstruction(currentPC, opcode, $"PHA");
                Push(A);
                break;

            case 0x28:
                LogInstruction(currentPC, opcode, $"PLP");
                var plpFlags = PopByte();
                SetFlagsFromByte((byte)(plpFlags & 0xEF | 0x20));
                UpdatePFromFlags();
                break;

            case 0x30:
                var bmiOffset = (ushort)Memory.Read(PC++);
                var bmiTarget = (ushort)(PC + bmiOffset);
                LogInstruction(currentPC, opcode, $"BMI ${bmiTarget:X4}", bmiOffset);

                if (N)
                    PC = bmiTarget;
                break;

            case 0x09:
                var oraValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"ORA #${oraValue:X2}", oraValue);

                A |= oraValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xb8:
                LogInstruction(currentPC, opcode, $"CLV");
                V = false;
                UpdatePFromFlags();
                break;

            case 0x49:
                var eorValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"EOR #${eorValue:X2}", eorValue);

                A ^= eorValue;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x69:
                var adcValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"ADC #${adcValue:X2}", adcValue);

                ushort adcSum = (ushort)(A + adcValue + (C ? 1 : 0));
                C = adcSum > 0xFF;
                V = (~(A ^ adcValue) & (A ^ adcSum) & 0x80) != 0;
                A = (byte)(adcSum & 0xFF);
                Z = A == 0;
                N = (A & 0x80) != 0;
                UpdatePFromFlags();
                break;

            case 0xc0:
                var cpyValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CPY #${cpyValue:X2}", cpyValue);

                var cpyResult = (byte)(Y - cpyValue);
                Z = Y == cpyValue;
                C = Y >= cpyValue;
                N = (cpyResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xe0:
                var cpxValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CPX #${cpxValue:X2}", cpxValue);

                var cpxResult = (byte)(X - cpxValue);
                Z = X == cpxValue;
                C = X >= cpxValue;
                N = (cpxResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xe9:
                var sbcValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"SBC #${sbcValue:X2}", sbcValue);

                var sbcCarryIn = C ? 1 : 0;
                var sbcVal = sbcValue ^ 0xFF; // one's complement
                var sbcSum = A + sbcVal + sbcCarryIn;

                // V flag: if sign(A) != sign(value) && sign(A) != sign(result)
                var sbcSignedOverflow = ((A ^ sbcValue) & (A ^ sbcSum) & 0x80) != 0;

                C = (sbcSum & 0x100) != 0; // carry means no borrow
                V = sbcSignedOverflow;
                A = (byte)(sbcSum & 0xFF);
                Z = A == 0;
                N = (A & 0x80) != 0;
                UpdatePFromFlags();
                break;

            case 0xc8:
                LogInstruction(currentPC, opcode, $"INY");

                Y++;
                Z = Y == 0;
                N = (Y & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x88:
                LogInstruction(currentPC, opcode, $"DEY");

                Y--;
                Z = Y == 0;
                N = (Y & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xe8:
                LogInstruction(currentPC, opcode, $"INX");

                X++;
                Z = X == 0;
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xca:
                LogInstruction(currentPC, opcode, $"DEX");

                X--;
                Z = X == 0;
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xa8:
                LogInstruction(currentPC, opcode, $"TAY");

                Y = A;
                Z = Y == 0;
                N = (Y & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xaa:
                LogInstruction(currentPC, opcode, $"TAX");

                X = A;
                Z = X == 0;
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x98:
                LogInstruction(currentPC, opcode, $"TYA");

                A = Y;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x8a:
                LogInstruction(currentPC, opcode, $"TXA");

                A = X;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xba:
                LogInstruction(currentPC, opcode, $"TSX");

                X = SP;
                Z = X == 0;
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x8e:
                var stx2Low = (ushort)Memory.Read(PC++);
                var stx2High = (ushort)Memory.Read(PC++);
                var stx2Address = (ushort)(stx2Low | (stx2High << 8));
                var stx2Value = Memory.Read(stx2Address);
                LogInstruction(currentPC, opcode, $"STX ${stx2Address:X4} = {stx2Value:X2}", stx2Low, stx2High);

                Memory.Write(stx2Address, X);
                break;

            case 0x9a:
                LogInstruction(currentPC, opcode, $"TXS");
                SP = X;
                break;

            case 0xae:
                var ldx2Low = (ushort)Memory.Read(PC++);
                var ldx2High = (ushort)Memory.Read(PC++);
                var ldx2Address = (ushort)(ldx2Low | (ldx2High << 8));
                var ldx2Value = Memory.Read(ldx2Address);
                LogInstruction(currentPC, opcode, $"LDX ${ldx2Address:X4} = {ldx2Value:X2}", ldx2Low, ldx2High);

                X = ldx2Value;
                Z = X == 0;
                N = (X & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xad:
                var lda2Low = (ushort)Memory.Read(PC++);
                var lda2High = (ushort)Memory.Read(PC++);
                var lda2Address = (ushort)(lda2Low | (lda2High << 8));
                var lda2Value = Memory.Read(lda2Address);
                LogInstruction(currentPC, opcode, $"LDA ${lda2Address:X4} = {lda2Value:X2}", lda2Low, lda2High);

                A = lda2Value;
                Z = A == 0;
                N = (A & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            default:
                throw new NotImplementedException($"Unimplemented opcode at {currentPC:X}: {opcode:X}");
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

    private void SetFlagsFromByte(byte value)
    {
        N = (value & 0x80) != 0;
        V = (value & 0x40) != 0;
        B = false; // never set by PLP or RTI
        D = (value & 0x08) != 0;
        I = (value & 0x04) != 0;
        Z = (value & 0x02) != 0;
        C = (value & 0x01) != 0;

    }

    public void LogInstruction(ushort pc, ushort opcode, string instruction, params ushort[] operands)
    {
        // ABCD  AB CD EF  OP $ABCD                        [registers]
        Console.WriteLine($"{pc,-6:X}{$"{opcode:X2} {string.Join(" ", operands.Select(op => op.ToString("X2")))}",-10}{instruction,-32}{GetRegisters()}");
    }

    public string GetRegisters() => $"A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{SP:X2}";
}
