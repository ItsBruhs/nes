namespace Nes;

public class Memory
{
    public byte[] Raw;

    // Header
    public int PrgRomSizeKb => Raw[4] * 16;
    public int ChrRomSizeKb => Raw[5] * 8;
    public bool HasTrainer => (Raw[6] & 0b00000100) != 0;
    public int Mapper => (Raw[6] >> 4) | (Raw[7] & 0xF0);
    public bool VerticalMirroring => (Raw[6] & 0b00000001) != 0;
    public bool BatteryBacked => (Raw[6] & 0b00000010) != 0;

    // Actual content
    public byte[]? Trainer;
    public byte[] PrgRom;
    public byte[] ChrRom;

    // Memory layout
    // 0000–07FF: 2 KB RAM
    // 0800–1FFF: Mirrors of 0000–07FF (every 0x800 bytes)
    // 2000–2007: PPU registers
    // 2008–3FFF: Mirrors of 2000–2007 (every 8 bytes)
    // 4000–4017: APU & I/O
    // 4018–401F: Test mode / unused
    // 4020–FFFF: Cartridge PRG ROM/RAM
    public byte[] Ram = new byte[2048];

    private int read2002Counter = 0;

    public Memory(byte[] raw)
    {
        Raw = raw;

        Console.WriteLine($"Prg rom size: {PrgRomSizeKb}KB");
        Console.WriteLine($"Chr rom size: {ChrRomSizeKb}KB");
        Console.WriteLine($"Has trainer: {HasTrainer}");
        Console.WriteLine($"Mapper: {Mapper}");
        Console.WriteLine($"Vertical mirroring: {VerticalMirroring}");
        Console.WriteLine($"Battery backed: {BatteryBacked}");

        int offset = 16; // skip 16 byte header

        if (HasTrainer)
        {
            Trainer = raw.Skip(offset).Take(512).ToArray();
            offset += 512;
            Console.WriteLine("Trainer found");
        }
        else
        {
            Console.WriteLine("No trainer");
        }

        PrgRom = raw.Skip(offset).Take(1024 * PrgRomSizeKb).ToArray();
        Console.WriteLine($"Prg rom length: {PrgRom.Length} bytes");
        Console.WriteLine($"Mapped range: 0x8000–0x{0x8000 + PrgRom.Length - 1:X4}");
        offset += 1024 * PrgRomSizeKb;

        ChrRom = raw.Skip(offset).Take(1024 * ChrRomSizeKb).ToArray();

        Console.WriteLine($"Prg rom: {PrgRomSizeKb}KB");
        Console.WriteLine($"Chr rom: {ChrRomSizeKb}KB");
    }

    // Zero Page, X-indexed indirect: ($addr,X)
    public ushort ReadIndirectX(byte zpAddr, byte x)
    {
        byte ptr = (byte)(zpAddr + x);
        byte low = Read(ptr);
        byte high = Read((byte)((ptr + 1) & 0xFF)); // Wrap around zero page
        return (ushort)(low | (high << 8));
    }

    // Zero Page indirect, Y-indexed: ($addr),Y
    public ushort ReadIndirectY(byte zpAddr, byte y, out byte low, out byte high, out ushort baseAddr)
    {
        low = Read(zpAddr);
        high = Read((byte)((zpAddr + 1) & 0xFF)); // Wrap zero page
        baseAddr = (ushort)(low | (high << 8));
        return (ushort)(baseAddr + y);
    }

    public byte Read(ushort address)
    {
        if (address < 0x2000)
        {
            return Ram[address % 0x800];
        }

        // Simulate PPU
        if (address >= 0x2000 && address <= 0x3FFF)
        {
            ushort reg = (ushort)(address % 8);

            switch (reg)
            {
                case 2: // PPUSTATUS
                    read2002Counter++;

                    // 0x80 for VBlank flag set
                    if (read2002Counter > 5)
                        return 0x80;
                    else
                        return 0x00;

                default:
                    return 0;
            }
        }

        if (address >= 0x8000)
        {
            var offset = address - 0x8000;

            if (PrgRom.Length == 0x4000)
                offset %= 0x4000;

            return PrgRom[offset];
        }

        return 0;
    }

    public void Write(ushort address, byte value)
    {
        if (address < 0x2000)
        {
            Ram[address % 0x800] = value;
        }
        else if (address >= 0x8000)
        {
            var offset = address - 0x8000;

            if (PrgRom.Length == 0x4000)
                // Mirror 16 KB prg rom across 32 KB space
                offset %= 0x4000;

            PrgRom[offset] = value;
        }
    }
}
