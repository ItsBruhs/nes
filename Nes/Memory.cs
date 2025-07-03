namespace Nes;

public class Memory
{
    public byte[] Raw;

    // Header
    public int PrgRomSizeKb => Raw[4] * 16;
    public int ChrRomSizeKb => Raw[5] * 8;
    public bool HasTrainer => (Raw[6] & 0b00000100) != 0;
    public int MapperId => (Raw[6] >> 4) | (Raw[7] & 0xF0);
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

    public Mapper Mapper;

    private Ppu Ppu;
    private Apu Apu;

    private Controller Controller1;
    private Controller Controller2;

    public Memory(byte[] raw, Ppu ppu, Apu apu, Controller controller1, Controller controller2)
    {
        Raw = raw;
        Ppu = ppu;
        Apu = apu;
        Controller1 = controller1;
        Controller2 = controller2;

        Console.WriteLine($"Prg rom size: {PrgRomSizeKb}KB");
        Console.WriteLine($"Chr rom size: {ChrRomSizeKb}KB");
        Console.WriteLine($"Has trainer: {HasTrainer}");
        Console.WriteLine($"Mapper: {MapperId}");
        Console.WriteLine($"Vertical mirroring: {VerticalMirroring}");
        Console.WriteLine($"Battery backed: {BatteryBacked}");

        int offset = 16; // skip 16 byte header

        if (HasTrainer)
        {
            Trainer = raw.Skip(offset).Take(512).ToArray();
            offset += 512;
        }

        PrgRom = raw.Skip(offset).Take(1024 * PrgRomSizeKb).ToArray();
        Console.WriteLine($"Prg rom length: {PrgRom.Length} bytes");
        Console.WriteLine($"Mapped range: 0x8000–0x{0x8000 + PrgRom.Length - 1:X4}");
        offset += 1024 * PrgRomSizeKb;

        ChrRom = raw.Skip(offset).Take(1024 * ChrRomSizeKb).ToArray();

        Mapper = MapperId switch
        {
            0 => new Mapper0Nrom(PrgRom, ChrRom),
            2 => new Mapper2UxRom(PrgRom, ChrRom),
            4 => new Mapper4Mmc3(PrgRom, ChrRom),
            _ => throw new NotSupportedException($"Mapper {MapperId} not supported")
        };
    }

    public ushort ReadIndirectX(byte zpAddr, byte x)
    {
        byte ptr = (byte)(zpAddr + x);
        byte low = Read(ptr);
        byte high = Read((byte)((ptr + 1) & 0xFF)); // Wrap around zero page
        return (ushort)(low | (high << 8));
    }

    public ushort ReadIndirectY(byte zpAddr, byte y, out byte low, out byte high, out ushort baseAddr)
    {
        low = Read(zpAddr);
        high = Read((byte)((zpAddr + 1) & 0xFF)); // Wrap zero page
        baseAddr = (ushort)(low | (high << 8));
        return (ushort)(baseAddr + y);
    }

    public byte Read(ushort address)
    {
        if (address == 0x4016)
            return Controller1.Read();
        if (address == 0x4017)
            return Controller2.Read();

        if (address < 0x2000)
            return Ram[address % 0x800];

        if (address >= 0x2000 && address <= 0x3FFF)
            return Ppu.ReadRegister((ushort)(address % 8)); // $2000–$2007 mirrored

        if (address >= 0x6000)
            return Mapper.CpuRead(address);

        return 0;
    }

    public void Write(ushort address, byte value)
    {
        if (address == 0x4016)
        {
            Controller1.Write(value);
            Controller2.Write(value);
        }

        if (address < 0x2000)
        {
            Ram[address % 0x800] = value;
        }
        else if (address >= 0x2000 && address <= 0x3FFF)
        {
            Ppu.WriteRegister((ushort)(address % 8), value);
        }
        else if (address >= 0x4000 && address <= 0x4017)
        {
            Apu.Write(address, value);
        }
        else if (address >= 0x6000)
        {
            Mapper.CpuWrite(address, value);
        }
    }
}
