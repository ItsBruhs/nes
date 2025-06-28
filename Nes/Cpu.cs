namespace Nes;

public class Cpu(Memory memory)
{
    private record Instruction(string Mnemonic,
        AddressingMode Mode,
        Action<AddressingResult>? Handler,
        Action? NoOperand = null,
        bool NoLogAbsoluteValue = false);

    public class AddressingResult
    {
        public ushort Address;
        public byte Value;
        public byte ZpAddress;
        public byte Low;
        public byte High;
        public ushort BaseAddress;
    }

    public enum AddressingMode
    {
        Immediate,
        ZeroPage,
        ZeroPageX,
        ZeroPageY,
        Absolute,
        AbsoluteX,
        AbsoluteY,
        Indirect,
        IndirectX,
        IndirectY,
    }

    // Registers
    public byte A, X, Y, SP, P;

    // Flags
    public bool C, Z, I, D, B, V, N;

    // Program counter
    public ushort PC;

    public Memory Memory = memory;

    public bool PrintLog = false;

    public string InstructionLog = "";

    private Dictionary<byte, Instruction> _instructionTable = [];

    public void Reset(ushort? pc = null)
    {
        InitInstructionTable();

        PC = pc ?? (ushort)(Memory.Read(0xFFFC) | (Memory.Read(0xFFFD) << 8));

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
        byte low = Pop();
        byte high = Pop();
        return (ushort)((high << 8) | low);
    }

    private void InitInstructionTable()
    {
        _instructionTable = new()
        {
            // NOP
            [0xEA] = new("NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0xEA, "NOP")),

            // NOP (undocumented)
            [0x1A] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0x1A, "*NOP")),
            [0x3A] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0x3A, "*NOP")),
            [0x5A] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0x5A, "*NOP")),
            [0x7A] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0x7A, "*NOP")),
            [0xDA] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0xDA, "*NOP")),
            [0xFA] = new("*NOP", AddressingMode.Immediate, null, () => LogInstruction((ushort)(PC - 1), 0xFA, "*NOP")),
            [0x80] = new("*NOP", AddressingMode.Immediate, addr => { }),
            [0x04] = new("*NOP", AddressingMode.ZeroPage, addr => { }),
            [0x44] = new("*NOP", AddressingMode.ZeroPage, addr => { }),
            [0x64] = new("*NOP", AddressingMode.ZeroPage, addr => { }),
            [0x14] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0x34] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0x54] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0x74] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0xD4] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0xF4] = new("*NOP", AddressingMode.ZeroPageX, addr => { }),
            [0x0C] = new("*NOP", AddressingMode.Absolute, addr => { }),
            [0x1C] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),
            [0x3C] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),
            [0x5C] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),
            [0x7C] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),
            [0xDC] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),
            [0xFC] = new("*NOP", AddressingMode.AbsoluteX, addr => { }),

            // LDA
            [0xA9] = new("LDA", AddressingMode.Immediate, LDA),
            [0xA5] = new("LDA", AddressingMode.ZeroPage, LDA),
            [0xB5] = new("LDA", AddressingMode.ZeroPageX, LDA),
            [0xAD] = new("LDA", AddressingMode.Absolute, LDA),
            [0xBD] = new("LDA", AddressingMode.AbsoluteX, LDA),
            [0xB9] = new("LDA", AddressingMode.AbsoluteY, LDA),
            [0xA1] = new("LDA", AddressingMode.IndirectX, LDA),
            [0xB1] = new("LDA", AddressingMode.IndirectY, LDA),

            // LDX
            [0xA2] = new("LDX", AddressingMode.Immediate, LDX),
            [0xA6] = new("LDX", AddressingMode.ZeroPage, LDX),
            [0xB6] = new("LDX", AddressingMode.ZeroPageY, LDX),
            [0xAE] = new("LDX", AddressingMode.Absolute, LDX),
            [0xBE] = new("LDX", AddressingMode.AbsoluteY, LDX),

            // LDY
            [0xA0] = new("LDY", AddressingMode.Immediate, LDY),
            [0xA4] = new("LDY", AddressingMode.ZeroPage, LDY),
            [0xB4] = new("LDY", AddressingMode.ZeroPageX, LDY),
            [0xAC] = new("LDY", AddressingMode.Absolute, LDY),
            [0xBC] = new("LDY", AddressingMode.AbsoluteX, LDY),

            // STA
            [0x85] = new("STA", AddressingMode.ZeroPage, STA),
            [0x95] = new("STA", AddressingMode.ZeroPageX, STA),
            [0x8D] = new("STA", AddressingMode.Absolute, STA),
            [0x9D] = new("STA", AddressingMode.AbsoluteX, STA),
            [0x99] = new("STA", AddressingMode.AbsoluteY, STA),
            [0x81] = new("STA", AddressingMode.IndirectX, STA),
            [0x91] = new("STA", AddressingMode.IndirectY, STA),

            // STX
            [0x86] = new("STX", AddressingMode.ZeroPage, STX),
            [0x96] = new("STX", AddressingMode.ZeroPageY, STX),
            [0x8E] = new("STX", AddressingMode.Absolute, STX),

            // STY
            [0x84] = new("STY", AddressingMode.ZeroPage, STY),
            [0x94] = new("STY", AddressingMode.ZeroPageX, STY),
            [0x8C] = new("STY", AddressingMode.Absolute, STY),

            // Flags
            [0x78] = new("SEI", AddressingMode.Immediate, null, () => SetFlag(ref I, true, "SEI", (ushort)(PC - 1), 0x78)),
            [0xB8] = new("CLV", AddressingMode.Immediate, null, () => SetFlag(ref V, false, "CLV", (ushort)(PC - 1), 0xB8)),
            [0xF8] = new("SED", AddressingMode.Immediate, null, () => SetFlag(ref D, true, "SED", (ushort)(PC - 1), 0xF8)),
            [0xD8] = new("CLD", AddressingMode.Immediate, null, () => SetFlag(ref D, false, "CLD", (ushort)(PC - 1), 0xD8)),
            [0x38] = new("SEC", AddressingMode.Immediate, null, () => SetFlag(ref C, true, "SEC", (ushort)(PC - 1), 0x38)),
            [0x18] = new("CLC", AddressingMode.Immediate, null, () => SetFlag(ref C, false, "CLC", (ushort)(PC - 1), 0x18)),

            // Inc/dec
            [0xE8] = new("INX", AddressingMode.Immediate, null, () => IncOrDec(ref X, false, "INX", (ushort)(PC - 1), 0xE8)),
            [0xCA] = new("DEX", AddressingMode.Immediate, null, () => IncOrDec(ref X, true, "DEX", (ushort)(PC - 1), 0xCA)),
            [0xC8] = new("INY", AddressingMode.Immediate, null, () => IncOrDec(ref Y, false, "INY", (ushort)(PC - 1), 0xC8)),
            [0x88] = new("DEY", AddressingMode.Immediate, null, () => IncOrDec(ref Y, true, "DEY", (ushort)(PC - 1), 0x88)),

            // Transfer
            [0xA8] = new("TAY", AddressingMode.Immediate, null, () => Transfer(ref A, ref Y, "TAY", (ushort)(PC - 1), 0xA8)),
            [0xAA] = new("TAX", AddressingMode.Immediate, null, () => Transfer(ref A, ref X, "TAX", (ushort)(PC - 1), 0xAA)),
            [0x98] = new("TYA", AddressingMode.Immediate, null, () => Transfer(ref Y, ref A, "TYA", (ushort)(PC - 1), 0x98)),
            [0x8A] = new("TXA", AddressingMode.Immediate, null, () => Transfer(ref X, ref A, "TXA", (ushort)(PC - 1), 0x8A)),
            [0x9A] = new("TXS", AddressingMode.Immediate, null, () => Transfer(ref X, ref SP, "TXS", (ushort)(PC - 1), 0x9A, false)),
            [0xBA] = new("TSX", AddressingMode.Immediate, null, () => Transfer(ref SP, ref X, "TSX", (ushort)(PC - 1), 0xBA)),

            // Branches
            [0xB0] = new("BCS", AddressingMode.Immediate, null, () => Branch(C, "BCS", (ushort)(PC - 1), 0xB0)),
            [0x90] = new("BCC", AddressingMode.Immediate, null, () => Branch(!C, "BCC", (ushort)(PC - 1), 0x90)),
            [0x70] = new("BVS", AddressingMode.Immediate, null, () => Branch(V, "BVS", (ushort)(PC - 1), 0x70)),
            [0x50] = new("BVC", AddressingMode.Immediate, null, () => Branch(!V, "BVC", (ushort)(PC - 1), 0x50)),
            [0xF0] = new("BEQ", AddressingMode.Immediate, null, () => Branch(Z, "BEQ", (ushort)(PC - 1), 0xF0)),
            [0xD0] = new("BNE", AddressingMode.Immediate, null, () => Branch(!Z, "BNE", (ushort)(PC - 1), 0xD0)),
            [0x30] = new("BMI", AddressingMode.Immediate, null, () => Branch(N, "BMI", (ushort)(PC - 1), 0x30)),
            [0x10] = new("BPL", AddressingMode.Immediate, null, () => Branch(!N, "BPL", (ushort)(PC - 1), 0x10)),

            // AND
            [0x29] = new("AND", AddressingMode.Immediate, AND),
            [0x25] = new("AND", AddressingMode.ZeroPage, AND),
            [0x35] = new("AND", AddressingMode.ZeroPageX, AND),
            [0x2D] = new("AND", AddressingMode.Absolute, AND),
            [0x3D] = new("AND", AddressingMode.AbsoluteX, AND),
            [0x39] = new("AND", AddressingMode.AbsoluteY, AND),
            [0x21] = new("AND", AddressingMode.IndirectX, AND),
            [0x31] = new("AND", AddressingMode.IndirectY, AND),

            // CMP
            [0xC9] = new("CMP", AddressingMode.Immediate, CMP),
            [0xC5] = new("CMP", AddressingMode.ZeroPage, CMP),
            [0xD5] = new("CMP", AddressingMode.ZeroPageX, CMP),
            [0xCD] = new("CMP", AddressingMode.Absolute, CMP),
            [0xDD] = new("CMP", AddressingMode.AbsoluteX, CMP),
            [0xD9] = new("CMP", AddressingMode.AbsoluteY, CMP),
            [0xC1] = new("CMP", AddressingMode.IndirectX, CMP),
            [0xD1] = new("CMP", AddressingMode.IndirectY, CMP),

            // CPX
            [0xE0] = new("CPX", AddressingMode.Immediate, CPX),
            [0xE4] = new("CPX", AddressingMode.ZeroPage, CPX),
            [0xEC] = new("CPX", AddressingMode.Absolute, CPX),

            // CPY
            [0xC0] = new("CPY", AddressingMode.Immediate, CPY),
            [0xC4] = new("CPY", AddressingMode.ZeroPage, CPY),
            [0xCC] = new("CPY", AddressingMode.Absolute, CPY),

            // JMP
            [0x4C] = new("JMP", AddressingMode.Absolute, null, JMP),
            [0x6C] = new("JMP", AddressingMode.Indirect, null, IndirectJMP),

            // JSR
            [0x20] = new("JSR", AddressingMode.Absolute, JSR, NoLogAbsoluteValue: true),

            // RTS
            [0x60] = new("RTS", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x60, "RTS"); RTS(); }),

            // RTI
            [0x40] = new("RTI", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x40, "RTI"); RTI(); }),

            // ORA
            [0x09] = new("ORA", AddressingMode.Immediate, ORA),
            [0x05] = new("ORA", AddressingMode.ZeroPage, ORA),
            [0x15] = new("ORA", AddressingMode.ZeroPageX, ORA),
            [0x0D] = new("ORA", AddressingMode.Absolute, ORA),
            [0x1D] = new("ORA", AddressingMode.AbsoluteX, ORA),
            [0x19] = new("ORA", AddressingMode.AbsoluteY, ORA),
            [0x01] = new("ORA", AddressingMode.IndirectX, ORA),
            [0x11] = new("ORA", AddressingMode.IndirectY, ORA),

            // EOR
            [0x49] = new("EOR", AddressingMode.Immediate, EOR),
            [0x45] = new("EOR", AddressingMode.ZeroPage, EOR),
            [0x55] = new("EOR", AddressingMode.ZeroPageX, EOR),
            [0x4D] = new("EOR", AddressingMode.Absolute, EOR),
            [0x5D] = new("EOR", AddressingMode.AbsoluteX, EOR),
            [0x59] = new("EOR", AddressingMode.AbsoluteY, EOR),
            [0x41] = new("EOR", AddressingMode.IndirectX, EOR),
            [0x51] = new("EOR", AddressingMode.IndirectY, EOR),

            // ADC
            [0x69] = new("ADC", AddressingMode.Immediate, ADC),
            [0x65] = new("ADC", AddressingMode.ZeroPage, ADC),
            [0x75] = new("ADC", AddressingMode.ZeroPageX, ADC),
            [0x6D] = new("ADC", AddressingMode.Absolute, ADC),
            [0x7D] = new("ADC", AddressingMode.AbsoluteX, ADC),
            [0x79] = new("ADC", AddressingMode.AbsoluteY, ADC),
            [0x61] = new("ADC", AddressingMode.IndirectX, ADC),
            [0x71] = new("ADC", AddressingMode.IndirectY, ADC),

            // SBC
            [0xE9] = new("SBC", AddressingMode.Immediate, SBC),
            [0xEB] = new("*SBC", AddressingMode.Immediate, SBC),
            [0xE5] = new("SBC", AddressingMode.ZeroPage, SBC),
            [0xF5] = new("SBC", AddressingMode.ZeroPageX, SBC),
            [0xED] = new("SBC", AddressingMode.Absolute, SBC),
            [0xFD] = new("SBC", AddressingMode.AbsoluteX, SBC),
            [0xF9] = new("SBC", AddressingMode.AbsoluteY, SBC),
            [0xE1] = new("SBC", AddressingMode.IndirectX, SBC),
            [0xF1] = new("SBC", AddressingMode.IndirectY, SBC),

            // PHP
            [0x08] = new("PHP", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x08, "PHP"); PHP(); }),

            // PLP
            [0x28] = new("PLP", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x28, "PLP"); PLP(); }),

            // PHA
            [0x48] = new("PHA", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x48, "PHA"); PHA(); }),

            // PLA
            [0x68] = new("PLA", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x68, "PLA"); PLA(); }),

            // LSR
            [0x4A] = new("LSR", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x4A, "LSR A"); LSR(); }),
            [0x46] = new("LSR", AddressingMode.ZeroPage, LSR),
            [0x56] = new("LSR", AddressingMode.ZeroPageX, LSR),
            [0x4E] = new("LSR", AddressingMode.Absolute, LSR),
            [0x5E] = new("LSR", AddressingMode.AbsoluteX, LSR),

            // ASL
            [0x0A] = new("ASL", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x0A, "ASL A"); ASL(); }),
            [0x06] = new("ASL", AddressingMode.ZeroPage, ASL),
            [0x16] = new("ASL", AddressingMode.ZeroPageX, ASL),
            [0x0E] = new("ASL", AddressingMode.Absolute, ASL),
            [0x1E] = new("ASL", AddressingMode.AbsoluteX, ASL),

            // ROR
            [0x6A] = new("ROR", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x6A, "ROR A"); ROR(); }),
            [0x66] = new("ROR", AddressingMode.ZeroPage, ROR),
            [0x76] = new("ROR", AddressingMode.ZeroPageX, ROR),
            [0x6E] = new("ROR", AddressingMode.Absolute, ROR),
            [0x7E] = new("ROR", AddressingMode.AbsoluteX, ROR),

            // ROL
            [0x2A] = new("ROL", AddressingMode.Immediate, null, () => { LogInstruction((ushort)(PC - 1), 0x2A, "ROL A"); ROL(); }),
            [0x26] = new("ROL", AddressingMode.ZeroPage, ROL),
            [0x36] = new("ROL", AddressingMode.ZeroPageX, ROL),
            [0x2E] = new("ROL", AddressingMode.Absolute, ROL),
            [0x3E] = new("ROL", AddressingMode.AbsoluteX, ROL),

            // INC
            [0xE6] = new("INC", AddressingMode.ZeroPage, INC),
            [0xF6] = new("INC", AddressingMode.ZeroPageX, INC),
            [0xEE] = new("INC", AddressingMode.Absolute, INC),
            [0xFE] = new("INC", AddressingMode.AbsoluteX, INC),

            // DEC
            [0xC6] = new("DEC", AddressingMode.ZeroPage, DEC),
            [0xD6] = new("DEC", AddressingMode.ZeroPageX, DEC),
            [0xCE] = new("DEC", AddressingMode.Absolute, DEC),
            [0xDE] = new("DEC", AddressingMode.AbsoluteX, DEC),

            // BIT
            [0x24] = new("BIT", AddressingMode.ZeroPage, BIT),
            [0x2C] = new("BIT", AddressingMode.Absolute, BIT),

            // LAX (undocumented)
            [0xA3] = new("*LAX", AddressingMode.IndirectX, LAX),
            [0xA7] = new("*LAX", AddressingMode.ZeroPage, LAX),
            [0xAF] = new("*LAX", AddressingMode.Absolute, LAX),
            [0xB3] = new("*LAX", AddressingMode.IndirectY, LAX),
            [0xB7] = new("*LAX", AddressingMode.ZeroPageY, LAX),
            [0xBF] = new("*LAX", AddressingMode.AbsoluteY, LAX),

            // SAX (undocumented)
            [0x87] = new("*SAX", AddressingMode.ZeroPage, addr => Memory.Write(addr.Address, (byte)(A & X))),
            [0x97] = new("*SAX", AddressingMode.ZeroPageY, addr => Memory.Write(addr.Address, (byte)(A & X))),
            [0x8F] = new("*SAX", AddressingMode.Absolute, addr => Memory.Write(addr.Address, (byte)(A & X))),
            [0x83] = new("*SAX", AddressingMode.IndirectX, addr => Memory.Write(addr.Address, (byte)(A & X))),

            // DCP (undocumented)
            [0xC3] = new("*DCP", AddressingMode.IndirectX, DCP),
            [0xC7] = new("*DCP", AddressingMode.ZeroPage, DCP),
            [0xCF] = new("*DCP", AddressingMode.Absolute, DCP),
            [0xD3] = new("*DCP", AddressingMode.IndirectY, DCP),
            [0xD7] = new("*DCP", AddressingMode.ZeroPageX, DCP),
            [0xDB] = new("*DCP", AddressingMode.AbsoluteY, DCP),
            [0xDF] = new("*DCP", AddressingMode.AbsoluteX, DCP),

            // ISB (undocumented)
            [0xE3] = new("*ISB", AddressingMode.IndirectX, ISB),
            [0xE7] = new("*ISB", AddressingMode.ZeroPage, ISB),
            [0xEF] = new("*ISB", AddressingMode.Absolute, ISB),
            [0xF3] = new("*ISB", AddressingMode.IndirectY, ISB),
            [0xF7] = new("*ISB", AddressingMode.ZeroPageX, ISB),
            [0xFB] = new("*ISB", AddressingMode.AbsoluteY, ISB),
            [0xFF] = new("*ISB", AddressingMode.AbsoluteX, ISB),

            // SLO (undocumented)
            [0x03] = new("*SLO", AddressingMode.IndirectX, SLO),
            [0x07] = new("*SLO", AddressingMode.ZeroPage, SLO),
            [0x0F] = new("*SLO", AddressingMode.Absolute, SLO),
            [0x13] = new("*SLO", AddressingMode.IndirectY, SLO),
            [0x17] = new("*SLO", AddressingMode.ZeroPageX, SLO),
            [0x1B] = new("*SLO", AddressingMode.AbsoluteY, SLO),
            [0x1F] = new("*SLO", AddressingMode.AbsoluteX, SLO),

            // RLA (undocumented)
            [0x23] = new("*RLA", AddressingMode.IndirectX, RLA),
            [0x27] = new("*RLA", AddressingMode.ZeroPage, RLA),
            [0x2F] = new("*RLA", AddressingMode.Absolute, RLA),
            [0x33] = new("*RLA", AddressingMode.IndirectY, RLA),
            [0x37] = new("*RLA", AddressingMode.ZeroPageX, RLA),
            [0x3B] = new("*RLA", AddressingMode.AbsoluteY, RLA),
            [0x3F] = new("*RLA", AddressingMode.AbsoluteX, RLA),

            // SRE (undocumented)
            [0x43] = new("*SRE", AddressingMode.IndirectX, SRE),
            [0x47] = new("*SRE", AddressingMode.ZeroPage, SRE),
            [0x4F] = new("*SRE", AddressingMode.Absolute, SRE),
            [0x53] = new("*SRE", AddressingMode.IndirectY, SRE),
            [0x57] = new("*SRE", AddressingMode.ZeroPageX, SRE),
            [0x5B] = new("*SRE", AddressingMode.AbsoluteY, SRE),
            [0x5F] = new("*SRE", AddressingMode.AbsoluteX, SRE),

            // RRA (undocumented)
            [0x63] = new("*RRA", AddressingMode.IndirectX, RRA),
            [0x67] = new("*RRA", AddressingMode.ZeroPage, RRA),
            [0x6F] = new("*RRA", AddressingMode.Absolute, RRA),
            [0x73] = new("*RRA", AddressingMode.IndirectY, RRA),
            [0x77] = new("*RRA", AddressingMode.ZeroPageX, RRA),
            [0x7B] = new("*RRA", AddressingMode.AbsoluteY, RRA),
            [0x7F] = new("*RRA", AddressingMode.AbsoluteX, RRA),
        };
    }

    public void Step()
    {
        byte opcode = Memory.Read(PC++);

        if (!_instructionTable.TryGetValue(opcode, out var instr))
            throw new NotImplementedException($"Unimplemented opcode: {opcode:X2} @ {PC:X4}");

        ushort currentPC = (ushort)(PC - 1);

        if (instr.Handler != null)
        {
            var operand = ReadOperand(instr.Mode);
            Log(instr.Mnemonic, currentPC, opcode, operand, instr.Mode, instr);
            instr.Handler(operand);
        }
        else
        {
            instr.NoOperand?.Invoke();
        }
    }

    private AddressingResult ReadOperand(AddressingMode mode)
    {
        switch (mode)
        {
            case AddressingMode.Immediate:
                byte value = Memory.Read(PC++);
                return new AddressingResult { Value = value };

            case AddressingMode.ZeroPage:
                ushort addr = Memory.Read(PC++);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr) };

            case AddressingMode.ZeroPageX:
                byte zpAddr = Memory.Read(PC++);
                addr = (byte)(zpAddr + X);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), ZpAddress = zpAddr };

            case AddressingMode.ZeroPageY:
                zpAddr = Memory.Read(PC++);
                addr = (byte)(zpAddr + Y);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), ZpAddress = zpAddr };

            case AddressingMode.Absolute:
                addr = Read16(out byte low, out byte high);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), Low = low, High = high };

            case AddressingMode.AbsoluteX:
                addr = (ushort)(Read16(out low, out high) + X);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), Low = low, High = high };

            case AddressingMode.AbsoluteY:
                addr = (ushort)(Read16(out low, out high) + Y);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), Low = low, High = high };

            case AddressingMode.Indirect: // Only used by JMP
                ushort ptr = Read16(out low, out high);
                // Simulate 6502 page wrap bug
                byte lo = Memory.Read(ptr);
                byte hi = Memory.Read((ushort)((ptr & 0xFF00) | ((ptr + 1) & 0x00FF)));
                addr = (ushort)(lo | (hi << 8));
                return new AddressingResult { Address = addr, Low = low, High = high };

            case AddressingMode.IndirectX:
                zpAddr = Memory.Read(PC++);
                addr = Memory.ReadIndirectX(zpAddr, X);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), ZpAddress = zpAddr };

            case AddressingMode.IndirectY:
                zpAddr = Memory.Read(PC++);
                addr = Memory.ReadIndirectY(zpAddr, Y, out low, out high, out ushort baseAddr);
                return new AddressingResult { Address = addr, Value = Memory.Read(addr), ZpAddress = zpAddr, Low = low, High = high, BaseAddress = baseAddr };

            default:
                throw new NotImplementedException($"Unknown addressing mode: {mode}");
        }
    }

    private void Log(string name, ushort pc, byte opcode, AddressingResult addr, AddressingMode mode, Instruction instr)
    {
        switch (mode)
        {
            case AddressingMode.Immediate:
                LogInstruction(pc, opcode, $"{name} #${addr.Value:X2}", addr.Value);
                break;
            case AddressingMode.ZeroPage:
                LogInstruction(pc, opcode, $"{name} ${addr.Address:X2} = {addr.Value:X2}", addr.Address);
                break;
            case AddressingMode.ZeroPageX:
                LogInstruction(pc, opcode, $"{name} ${addr.ZpAddress:X2},X @ {addr.Address:X2} = {addr.Value:X2}", addr.ZpAddress);
                break;
            case AddressingMode.ZeroPageY:
                LogInstruction(pc, opcode, $"{name} ${addr.ZpAddress:X2},Y @ {addr.Address:X2} = {addr.Value:X2}", addr.ZpAddress);
                break;
            case AddressingMode.Absolute:
                if (instr.NoLogAbsoluteValue)
                    LogInstruction(pc, opcode, $"{name} ${addr.Address:X4}", addr.Low, addr.High);
                else
                    LogInstruction(pc, opcode, $"{name} ${addr.Address:X4} = {addr.Value:X2}", addr.Low, addr.High);
                break;
            case AddressingMode.AbsoluteX:
                LogInstruction(pc, opcode, $"{name} ${addr.High:X2}{addr.Low:X2},X @ {addr.Address:X4} = {addr.Value:X2}", addr.Low, addr.High);
                break;
            case AddressingMode.AbsoluteY:
                LogInstruction(pc, opcode, $"{name} ${addr.High:X2}{addr.Low:X2},Y @ {addr.Address:X4} = {addr.Value:X2}", addr.Low, addr.High);
                break;
            case AddressingMode.IndirectX:
                LogInstruction(pc, opcode, $"{name} (${addr.ZpAddress:X2},X) @ {(byte)(addr.ZpAddress + X):X2} = {addr.Address:X4} = {addr.Value:X2}", addr.ZpAddress);
                break;
            case AddressingMode.IndirectY:
                LogInstruction(pc, opcode, $"{name} (${addr.ZpAddress:X2}),Y = {addr.BaseAddress:X4} @ {addr.Address:X4} = {addr.Value:X2}", addr.ZpAddress);
                break;
        }
    }

    private void LDA(AddressingResult operand)
    {
        A = operand.Value;
        SetZN(A);
    }

    private void LDX(AddressingResult operand)
    {
        X = operand.Value;
        SetZN(X);
    }

    private void LDY(AddressingResult operand)
    {
        Y = operand.Value;
        SetZN(Y);
    }

    private void STA(AddressingResult operand) => Memory.Write(operand.Address, A);
    private void STX(AddressingResult operand) => Memory.Write(operand.Address, X);
    private void STY(AddressingResult operand) => Memory.Write(operand.Address, Y);

    private void AND(AddressingResult addr)
    {
        A &= addr.Value;
        SetZN(A);
    }

    private void CMP(AddressingResult addr)
    {
        byte result = (byte)(A - addr.Value);
        Z = A == addr.Value;
        C = A >= addr.Value;
        N = (result & (1 << 7)) != 0;
        UpdatePFromFlags();
    }

    private void CPX(AddressingResult addr)
    {
        byte result = (byte)(X - addr.Value);
        Z = X == addr.Value;
        C = X >= addr.Value;
        N = (result & (1 << 7)) != 0;
        UpdatePFromFlags();
    }

    private void CPY(AddressingResult addr)
    {
        byte result = (byte)(Y - addr.Value);
        Z = Y == addr.Value;
        C = Y >= addr.Value;
        N = (result & (1 << 7)) != 0;
        UpdatePFromFlags();
    }

    private void JMP()
    {
        var operand = ReadOperand(AddressingMode.Absolute);
        LogInstruction((ushort)(PC - 3), 0x4C, $"JMP ${operand.Address:X4}", operand.Low, operand.High);
        PC = operand.Address;
    }

    private void IndirectJMP()
    {
        var operand = ReadOperand(AddressingMode.Indirect);
        LogInstruction((ushort)(PC - 3), 0x6C, $"JMP (${operand.High:X2}{operand.Low:X2}) = {operand.Address:X4}", operand.Low, operand.High);
        PC = operand.Address;
    }

    private void JSR(AddressingResult addr)
    {
        Push((ushort)(PC - 1));
        PC = addr.Address;
    }

    private void RTS() => PC = (ushort)(Pop16() + 1);

    private void RTI()
    {
        SetFlagsFromByte(Pop());
        UpdatePFromFlags();
        PC = Pop16();
    }

    private void ORA(AddressingResult addr)
    {
        A |= addr.Value;
        SetZN(A);
    }

    private void EOR(AddressingResult addr)
    {
        A ^= addr.Value;
        SetZN(A);
    }

    private void ADC(AddressingResult addr)
    {
        ushort sum = (ushort)(A + addr.Value + (C ? 1 : 0));
        C = sum > 0xFF;
        V = (~(A ^ addr.Value) & (A ^ sum) & 0x80) != 0;
        A = (byte)(sum & 0xFF);
        SetZN(A);
    }

    private void SBC(AddressingResult addr)
    {
        int carryIn = C ? 1 : 0;
        int value = addr.Value ^ 0xFF; // One's complement
        int intSum = A + value + carryIn;

        // V flag: if sign(A) != sign(value) && sign(A) != sign(result)
        bool signedOverflow = ((A ^ addr.Value) & (A ^ intSum) & 0x80) != 0;

        C = (intSum & 0x100) != 0; // Carry means no borrow
        V = signedOverflow;
        A = (byte)(intSum & 0xFF);
        SetZN(A);
    }

    private void PHP() => Push(GetProcessorStatusForStack(true));

    private void PLP()
    {
        byte flags = Pop();
        SetFlagsFromByte((byte)(flags & 0xEF | 0x20)); // Clear B, set U
        UpdatePFromFlags(); // Regenerate correct P from flags
    }

    private void PHA() => Push(A);

    private void PLA()
    {
        A = Pop();
        SetZN(A);
    }

    private void LSR()
    {
        C = (A & 0x01) != 0; // Store bit 0 in carry
        A >>= 1; // Shift right
        SetZN(A);
    }

    private void LSR(AddressingResult addr)
    {
        C = (addr.Value & 0x01) != 0; // Bit 0 -> carry
        byte result = (byte)(addr.Value >> 1);
        Memory.Write(addr.Address, result);
        SetZN(result);
    }

    private void ASL()
    {
        C = (A & 0x80) != 0; // Store bit 7 in carry
        A <<= 1; // Shift left
        SetZN(A);
    }

    private void ASL(AddressingResult addr)
    {
        C = (addr.Value & 0x80) != 0; // Bit 7 to carry
        addr.Value <<= 1;
        Memory.Write(addr.Address, addr.Value);
        SetZN(addr.Value);
    }

    private void ROR()
    {
        bool oldCarry = C;
        C = (A & 0x01) != 0; // Store bit 0 in carry
        A = (byte)((A >> 1) | (oldCarry ? 0x80 : 0)); // Rotate right
        SetZN(A);
    }

    private void ROR(AddressingResult addr)
    {
        bool oldCarry = C;
        C = (addr.Value & 0x01) != 0;
        addr.Value = (byte)((addr.Value >> 1) | (oldCarry ? 0x80 : 0));
        Memory.Write(addr.Address, addr.Value);
        SetZN(addr.Value);
    }

    private void ROL()
    {
        bool oldCarry = C;
        C = (A & 0x80) != 0; // Store bit 7 in carry
        A = (byte)((A << 1) | (oldCarry ? 0x01 : 0)); // Rotate left
        SetZN(A);
    }

    private void ROL(AddressingResult addr)
    {
        bool oldCarry = C;
        C = (addr.Value & 0x80) != 0; // Bit 7 goes into carry
        addr.Value = (byte)((addr.Value << 1) | (oldCarry ? 1 : 0)); // Shift left, insert carry into bit 0
        Memory.Write(addr.Address, addr.Value);
        SetZN(addr.Value);
    }

    private void INC(AddressingResult addr)
    {
        byte result = (byte)(addr.Value + 1);
        Memory.Write(addr.Address, result);
        SetZN(result);
    }

    private void DEC(AddressingResult addr)
    {
        byte result = (byte)(addr.Value - 1);
        Memory.Write(addr.Address, result);
        SetZN(result);
    }

    private void BIT(AddressingResult addr)
    {
        Z = (A & addr.Value) == 0;
        N = (addr.Value & (1 << 7)) != 0;
        V = (addr.Value & (1 << 6)) != 0;
        UpdatePFromFlags();
    }

    private void LAX(AddressingResult addr)
    {
        A = X = addr.Value;
        SetZN(A);
    }

    private void DCP(AddressingResult addr)
    {
        byte newVal = (byte)(addr.Value - 1);
        Memory.Write(addr.Address, newVal);

        byte cmp = (byte)(A - newVal);
        C = A >= newVal;
        Z = A == newVal;
        N = (cmp & 0x80) != 0;
        UpdatePFromFlags();
    }

    private void ISB(AddressingResult addr)
    {
        byte value = (byte)(addr.Value + 1);
        Memory.Write(addr.Address, value);

        int carryIn = C ? 1 : 0;
        int subtract = value ^ 0xFF;
        int result = A + subtract + carryIn;

        V = ((A ^ value) & (A ^ result) & 0x80) != 0;
        C = (result & 0x100) != 0;
        A = (byte)(result & 0xFF);

        SetZN(A);
    }

    private void SLO(AddressingResult addr)
    {
        byte val = addr.Value;
        C = (val & 0x80) != 0; // Bit 7 to Carry
        val <<= 1; // Shift left
        Memory.Write(addr.Address, val);

        A |= val; // OR with accumulator
        SetZN(A);
    }

    private void RLA(AddressingResult addr)
    {
        byte original = addr.Value;
        bool oldCarry = C;

        C = (original & 0x80) != 0; // Bit 7 -> carry
        byte rotated = (byte)((original << 1) | (oldCarry ? 1 : 0));

        Memory.Write(addr.Address, rotated);

        A &= rotated;
        SetZN(A);
    }

    private void SRE(AddressingResult addr)
    {
        byte original = addr.Value;
        C = (original & 0x01) != 0; // Bit 0 to carry
        byte shifted = (byte)(original >> 1);
        Memory.Write(addr.Address, shifted);

        A ^= shifted;
        SetZN(A);
    }

    private void RRA(AddressingResult addr)
    {
        byte value = addr.Value;
        bool oldCarry = C;
        C = (value & 0x01) != 0; // Bit 0 to carry
        value >>= 1;
        if (oldCarry)
            value |= 0x80;

        Memory.Write(addr.Address, value);

        // ADC part
        ushort sum = (ushort)(A + value + (C ? 1 : 0));
        V = (~(A ^ value) & (A ^ sum) & 0x80) != 0;
        C = sum > 0xFF;
        A = (byte)(sum & 0xFF);
        SetZN(A);
    }

    private void SetFlag(ref bool flag, bool value, string mnemonic, ushort pc, byte opcode)
    {
        LogInstruction(pc, opcode, mnemonic);
        flag = value;
        UpdatePFromFlags();
    }

    private void IncOrDec(ref byte register, bool decrement, string mnemonic, ushort pc, byte opcode)
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
        byte low = Memory.Read(PC++);
        byte high = Memory.Read(PC++);
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
        if (!PrintLog)
            return;

        // PC    raw asm   OP $operands                    registers
        var text = $"{pc,-6:X4}{$"{opcode:X2} {string.Join(" ", operands.Select(op => op.ToString("X2")))}".PadRight(instruction.StartsWith('*') ? 9 : 10)}{instruction.PadRight(instruction.StartsWith('*') ? 33 : 32)}{GetRegisters()}";
        Console.WriteLine(text);
        InstructionLog += text + Environment.NewLine;
    }

    public string GetRegisters() => $"A:{A:X2} X:{X:X2} Y:{Y:X2} P:{P:X2} SP:{SP:X2}";
}
