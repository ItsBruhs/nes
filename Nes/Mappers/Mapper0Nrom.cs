namespace Nes;

public class Mapper0Nrom : Mapper
{
    private byte[] prgRom;
    private byte[] chrRom;

    public Mapper0Nrom(byte[] prgRom, byte[] chrRom)
    {
        this.prgRom = prgRom;
        this.chrRom = chrRom;
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x8000)
        {
            int offset = address - 0x8000;
            if (prgRom.Length == 0x4000)
                offset %= 0x4000;

            return prgRom[offset];
        }

        return 0;
    }

    public override void CpuWrite(ushort address, byte value) { }

    public override byte PpuRead(ushort address) => chrRom[address];

    public override void PpuWrite(ushort address, byte value) { }
}
