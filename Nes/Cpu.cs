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

    public string InstructionLog = "";

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

    private byte Pop()
    {
        SP++;
        return Memory.Read((ushort)(0x0100 + SP));
    }

    private ushort Pop16()
    {
        var low = Pop();
        var high = Pop();
        return (ushort)((high << 8) | low);
    }

    public void Step()
    {
        ushort currentPC = PC;
        var opcode = Memory.Read(PC++);

        switch (opcode)
        {
            case 0xea: LogInstruction(currentPC, opcode, "NOP"); break; // NOP - No Operation

            case 0xa2: LoadImmediate(ref X, "LDX", currentPC, opcode); break; // LDX - Load X Register
            case 0xa0: LoadImmediate(ref Y, "LDY", currentPC, opcode); break; // LDY - Load Y Register
            case 0xa9: LoadImmediate(ref A, "LDA", currentPC, opcode); break; // LDA - Load A Register

            case 0x78: SetFlag(ref I, true, "SEI", currentPC, opcode); break; // SEI - Set Interrupt Disable
            case 0xb8: SetFlag(ref V, false, "CLV", currentPC, opcode); break; // CLV - Clear Overflow Flag
            case 0xf8: SetFlag(ref D, true, "SED", currentPC, opcode); break; // SED - Set Decimal Flag
            case 0xd8: SetFlag(ref D, false, "CLD", currentPC, opcode); break; // CLD - Clear Decimal Mode
            case 0x38: SetFlag(ref C, true, "SEC", currentPC, opcode); break; // SEC - Set Carry Flag
            case 0x18: SetFlag(ref C, false, "CLC", currentPC, opcode); break; // CLC - Clear Carry Flag

            case 0xe8: IncrementOrDecrement(ref X, false, "INX", currentPC, opcode); break; // INX - Increment X Register
            case 0xca: IncrementOrDecrement(ref X, true, "DEX", currentPC, opcode); break; // DEX - Decrement X Register
            case 0xc8: IncrementOrDecrement(ref Y, false, "INY", currentPC, opcode); break; // INY - Increment Y Register
            case 0x88: IncrementOrDecrement(ref Y, true, "DEY", currentPC, opcode); break; // DEY - Decrement Y Register

            case 0xa8: Transfer(ref A, ref Y, "TAY", currentPC, opcode); break; // TAY - Transfer Accumulator to Y
            case 0xaa: Transfer(ref A, ref X, "TAX", currentPC, opcode); break; // TAX - Transfer Accumulator to X
            case 0x98: Transfer(ref Y, ref A, "TYA", currentPC, opcode); break; // TYA - Transfer Y to Accumulator
            case 0x8a: Transfer(ref X, ref A, "TXA", currentPC, opcode); break; // TXA - Transfer X to Accumulator
            case 0x9a: Transfer(ref X, ref SP, "TXS", currentPC, opcode, false); break; // TXS - Transfer X to Stack Pointer
            case 0xba: Transfer(ref SP, ref X, "TSX", currentPC, opcode); break; // TSX - Transfer Stack Pointer to X

            case 0xae: // LDX - Load X Register
                var ldxAddress = Read16(out var ldxLow, out var ldxHigh);
                var ldxValue = Memory.Read(ldxAddress);
                LogInstruction(currentPC, opcode, $"LDX ${ldxAddress:X4} = {ldxValue:X2}", ldxLow, ldxHigh);
                X = ldxValue;
                SetZN(X);
                break;

            case 0x86: // STX - Store X Register
            case 0x8e:
                ushort stxAddress;

                if (opcode == 0x86) // Zero page addressing mode
                {
                    stxAddress = Memory.Read(PC++);
                    LogInstruction(currentPC, opcode, $"STX ${stxAddress:X2} = {X:X2}", stxAddress);
                }
                else // Absolute addressing mode
                {
                    stxAddress = Read16(out var low, out var high);
                    LogInstruction(currentPC, opcode, $"STX ${stxAddress:X4} = {Memory.Read(stxAddress):X2}", low, high);
                }

                Memory.Write(stxAddress, X);
                break;

            case 0xad: // LDA - Load Accumulator
                var ldaAddress = Read16(out var ldaLow, out var ldaHigh);
                var ldaValue = Memory.Read(ldaAddress);
                LogInstruction(currentPC, opcode, $"LDA ${ldaAddress:X4} = {ldaValue:X2}", ldaLow, ldaHigh);
                A = ldaValue;
                SetZN(A);
                break;

            case 0x85: // STA - Store Accumulator
                var staAddress = (ushort)Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"STA ${staAddress:X2} = {Memory.Read(staAddress):X2}", staAddress);
                Memory.Write(staAddress, A);
                break;

            case 0xb0: Branch(C, "BCS", currentPC, opcode); break; // BCS - Branch if Carry Set
            case 0x90: Branch(!C, "BCC", currentPC, opcode); break; // BCC - Branch if Carry Clear
            case 0x70: Branch(V, "BVS", currentPC, opcode); break; // BVS - Branch if Overflow Set
            case 0x50: Branch(!V, "BVC", currentPC, opcode); break; // BVC - Branch if Overflow Clear
            case 0xf0: Branch(Z, "BEQ", currentPC, opcode); break; // BEQ - Branch if Equal
            case 0xd0: Branch(!Z, "BNE", currentPC, opcode); break; // BNE - Branch if Not Equal
            case 0x30: Branch(N, "BMI", currentPC, opcode); break; // BMI - Branch if Minus
            case 0x10: Branch(!N, "BPL", currentPC, opcode); break; // BPL - Branch if Positive

            case 0x4c: // JMP - Jump
                var jmpTarget = Read16(out var jmpLow, out var jmpHigh);
                LogInstruction(currentPC, opcode, $"JMP ${jmpTarget:X4}", jmpLow, jmpHigh);
                PC = jmpTarget;
                break;

            case 0x20: // JSR - Jump to Subroutine
                ushort jsrTarget = Read16(out var jsrLow, out var jsrHigh);
                LogInstruction(currentPC, opcode, $"JSR ${jsrTarget:X4}", jsrLow, jsrHigh);
                Push((ushort)(PC - 1));
                PC = jsrTarget;
                break;

            case 0x60: // RTS - Return from Subroutine
                LogInstruction(currentPC, opcode, "RTS");
                PC = (ushort)(Pop16() + 1);
                break;

            case 0x40: // RTI - Return from Interrupt
                LogInstruction(currentPC, opcode, "RTI");
                SetFlagsFromByte(Pop());
                UpdatePFromFlags();
                PC = Pop16();
                break;

            case 0xc9: // CMP - Compare
                var cmpValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CMP #${cmpValue:X2}", cmpValue);
                var cmpResult = (byte)(A - cmpValue);
                Z = A == cmpValue;
                C = A >= cmpValue;
                N = (cmpResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xe0: // CPX - Compare X Register
                var cpxValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CPX #${cpxValue:X2}", cpxValue);
                var cpxResult = (byte)(X - cpxValue);
                C = X >= cpxValue;
                Z = X == cpxValue;
                N = (cpxResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0xc0: // CPY - Compare Y Register
                var cpyValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"CPY #${cpyValue:X2}", cpyValue);
                var cpyResult = (byte)(Y - cpyValue);
                Z = Y == cpyValue;
                C = Y >= cpyValue;
                N = (cpyResult & (1 << 7)) != 0;
                UpdatePFromFlags();
                break;

            case 0x29: // AND - Logical AND
                var andValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"AND #${andValue:X2}", andValue);
                A &= andValue;
                SetZN(A);
                break;

            case 0x24: // BIT - Bit Test
                var bitAddress = Memory.Read(PC++);
                var bitValue = Memory.Read(bitAddress);
                LogInstruction(currentPC, opcode, $"BIT ${bitAddress:X2} = {bitValue:X2}", bitAddress);
                Z = (A & bitValue) == 0;
                N = (bitValue & (1 << 7)) != 0;
                V = (bitValue & (1 << 6)) != 0;
                UpdatePFromFlags();
                break;

            case 0x09: // ORA - Logical Inclusive OR
                var oraValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"ORA #${oraValue:X2}", oraValue);
                A |= oraValue;
                SetZN(A);
                break;

            case 0x49: // EOR - Exclusive OR
                var eorValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"EOR #${eorValue:X2}", eorValue);
                A ^= eorValue;
                SetZN(A);
                break;

            case 0x69: // ADC - Add with Carry
                var adcValue = Memory.Read(PC++);
                LogInstruction(currentPC, opcode, $"ADC #${adcValue:X2}", adcValue);
                ushort adcSum = (ushort)(A + adcValue + (C ? 1 : 0));
                C = adcSum > 0xFF;
                V = (~(A ^ adcValue) & (A ^ adcSum) & 0x80) != 0;
                A = (byte)(adcSum & 0xFF);
                SetZN(A);
                break;

            case 0xe9: // SBC - Subtract with Carry
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
                SetZN(A);
                break;

            case 0x08: // PHP - Push Processor Status
                LogInstruction(currentPC, opcode, "PHP");
                Push(GetProcessorStatusForStack(true));
                break;

            case 0x28: // PLP - Pull Processor Status
                LogInstruction(currentPC, opcode, "PLP");
                var flags = Pop();
                SetFlagsFromByte((byte)(flags & 0xEF | 0x20)); // Clear B, set U
                UpdatePFromFlags(); // Regenerate correct P from flags
                break;

            case 0x48: // PHA - Push Accumulator
                LogInstruction(currentPC, opcode, "PHA");
                Push(A);
                break;

            case 0x68: // PLA - Pull Accumulator
                LogInstruction(currentPC, opcode, "PLA");
                A = Pop();
                SetZN(A);
                break;

            default:
                throw new NotImplementedException($"Unimplemented opcode at {currentPC:X}: {opcode:X}");
        }
    }

    private void SetFlag(ref bool flag, bool value, string mnemonic, ushort pc, byte opcode)
    {
        LogInstruction(pc, opcode, mnemonic);
        flag = value;
        UpdatePFromFlags();
    }

    private void IncrementOrDecrement(ref byte register, bool decrement, string mnemonic, ushort pc, byte opcode)
    {
        LogInstruction(pc, opcode, mnemonic);
        if (decrement)
            register--;
        else
            register++;
        SetZN(register);
    }

    private void Transfer(ref byte src, ref byte dst, string mnemonic, ushort pc, byte opcode, bool setFlags = true)
    {
        LogInstruction(pc, opcode, mnemonic);
        dst = src;
        if (setFlags)
            SetZN(dst);
    }

    private ushort Read16(out byte low, out byte high)
    {
        low = Memory.Read(PC++);
        high = Memory.Read(PC++);
        return (ushort)(low | (high << 8));
    }

    private ushort Read16()
    {
        var low = Memory.Read(PC++);
        var high = Memory.Read(PC++);
        return (ushort)(low | (high << 8));
    }

    private void Branch(bool condition, string name, ushort pc, byte opcode)
    {
        byte offset = Memory.Read(PC++);
        ushort target = (ushort)(PC + (sbyte)offset); // signed offset
        LogInstruction(pc, opcode, $"{name} ${target:X4}", offset);
        if (condition)
            PC = target;
    }

    private void LoadImmediate(ref byte register, string name, ushort pc, byte opcode)
    {
        byte value = Memory.Read(PC++);
        LogInstruction(pc, opcode, $"{name} #${value:X2}", value);
        register = value;
        SetZN(register);
    }

    private void SetZN(byte value)
    {
        Z = value == 0;
        N = (value & 0x80) != 0;
        UpdatePFromFlags();
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

    private void UpdateFlagsFromP() => SetFlagsFromByte(P);

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

    private byte GetProcessorStatusForStack(bool breakFlag)
    {
        byte status = 0;
        if (N) status |= 1 << 7;
        if (V) status |= 1 << 6;
        status |= 1 << 5; // Unused flag is always set
        if (breakFlag) status |= 1 << 4;
        if (D) status |= 1 << 3;
        if (I) status |= 1 << 2;
        if (Z) status |= 1 << 1;
        if (C) status |= 1 << 0;
        return status;
    }

    public void LogInstruction(ushort pc, ushort opcode, string instruction, params ushort[] operands)
    {
        // PC    raw asm   OP $operands                    registers
        var text = $"{pc,-6:X}{$"{opcode:X2} {string.Join(" ", operands.Select(op => op.ToString("X2")))}",-10}{instruction,-32}{GetRegisters()}";
        Console.WriteLine(text);
        InstructionLog += text + Environment.NewLine;
    }

    public string GetRegisters() => $"A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{SP:X2}";
}
