namespace Nes;

public class Mapper2UxRom : Mapper
{
    private byte[] prgRom;
    private byte[] chr;
    private byte[] prgRam = new byte[0x2000];

    private int prgBankCount;
    private int selectedBank = 0;

    public Mapper2UxRom(byte[] prgRom, byte[] chr)
    {
        this.prgRom = prgRom;
        this.chr = chr;
        prgBankCount = prgRom.Length / 0x4000;
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x6000 && address < 0x8000)
            return prgRam[address - 0x6000];

        if (address >= 0x8000 && address < 0xC000)
        {
            int offset = address - 0x8000;
            return prgRom[(selectedBank * 0x4000) + offset];
        }

        if (address >= 0xC000 && address <= 0xFFFF)
        {
            int offset = address - 0xC000;
            return prgRom[((prgBankCount - 1) * 0x4000) + offset];
        }

        return 0;
    }

    public override void CpuWrite(ushort address, byte value)
    {
        if (address >= 0x6000 && address < 0x8000)
        {
            prgRam[address - 0x6000] = value;
            return;
        }

        if (address >= 0x8000)
        {
            selectedBank = value % prgBankCount;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address < 0x2000)
            return chr[address];

        return 0;
    }

    public override void PpuWrite(ushort address, byte value)
    {
        if (address < 0x2000 && chr != null && chr.Length == 0x2000)
            chr[address] = value;
    }
}
