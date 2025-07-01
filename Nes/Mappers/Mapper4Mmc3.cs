namespace Nes;

public class Mapper4Mmc3 : Mapper
{
    private byte[] prgRom;
    private byte[] chrRom;
    private byte[] prgRam = new byte[0x2000];

    private int prgBankCount;
    private int chrBankCount;

    private int[] prgBanks = new int[4];
    private int[] chrBanks = new int[8];

    private byte bankSelect;
    private byte[] bankRegs = new byte[8];

    private bool prgMode;
    private bool chrMode;

    private byte irqLatch;
    private byte irqCounter;
    private bool irqReload;
    private bool irqEnabled;
    public bool irqPending;

    public Mapper4Mmc3(byte[] prgRom, byte[] chrRom)
    {
        this.prgRom = prgRom;
        this.chrRom = chrRom;

        prgBankCount = prgRom.Length / 0x2000;
        chrBankCount = chrRom.Length / 0x0400;

        prgBanks[0] = 0;
        prgBanks[1] = 1;
        prgBanks[2] = prgBankCount - 2;
        prgBanks[3] = prgBankCount - 1;
    }

    public void ClockScanline()
    {
        if (irqReload || irqCounter == 0)
        {
            irqCounter = irqLatch;
            irqReload = false;
        }
        else
        {
            irqCounter--;
            if (irqCounter == 0 && irqEnabled)
            {
                irqPending = true;
            }
        }
    }

    public override byte CpuRead(ushort address)
    {
        if (address >= 0x6000 && address < 0x8000)
            return prgRam[address - 0x6000];

        if (address >= 0x8000 && address <= 0xFFFF)
        {
            int bankIndex = (address - 0x8000) / 0x2000;
            int offset = address % 0x2000;
            int bank = prgBanks[bankIndex];

            return prgRom[(bank * 0x2000) + offset];
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

        if (address >= 0x8000 && address <= 0x9FFF)
        {
            if ((address & 1) == 0)
            {
                bankSelect = value;
                prgMode = (value & 0x40) != 0;
                chrMode = (value & 0x80) != 0;
            }
            else
            {
                int reg = bankSelect & 0b111;
                bankRegs[reg] = value;
                UpdateBanks();
            }
        }
        else if (address >= 0xA000 && address <= 0xBFFF)
        {
            if ((address & 1) == 0)
            {
                // TODO: Mirroring control
            }
            else
            {
                // TODO: Prg ram protect
            }
        }
        else if (address >= 0xC000 && address <= 0xDFFF)
        {
            if ((address & 1) == 0)
            {
                irqLatch = value;
            }
            else
            {
                irqReload = true;
            }
        }
        else if (address >= 0xE000 && address <= 0xFFFF)
        {
            if ((address & 1) == 0)
            {
                irqEnabled = false;
                irqPending = false;
            }
            else
            {
                irqEnabled = true;
            }
        }
    }

    private void UpdateBanks()
    {
        if (prgMode)
        {
            prgBanks[0] = prgBankCount - 2;
            prgBanks[1] = bankRegs[7] % prgBankCount;
            prgBanks[2] = bankRegs[6] % prgBankCount;
            prgBanks[3] = prgBankCount - 1;
        }
        else
        {
            prgBanks[0] = bankRegs[6] % prgBankCount;
            prgBanks[1] = bankRegs[7] % prgBankCount;
            prgBanks[2] = prgBankCount - 2;
            prgBanks[3] = prgBankCount - 1;
        }

        if (!chrMode)
        {
            chrBanks[0] = (bankRegs[0] & 0xFE) % chrBankCount;
            chrBanks[1] = (bankRegs[0] | 0x01) % chrBankCount;
            chrBanks[2] = (bankRegs[1] & 0xFE) % chrBankCount;
            chrBanks[3] = (bankRegs[1] | 0x01) % chrBankCount;
            chrBanks[4] = bankRegs[2] % chrBankCount;
            chrBanks[5] = bankRegs[3] % chrBankCount;
            chrBanks[6] = bankRegs[4] % chrBankCount;
            chrBanks[7] = bankRegs[5] % chrBankCount;
        }
        else
        {
            chrBanks[4] = (bankRegs[0] & 0xFE) % chrBankCount;
            chrBanks[5] = (bankRegs[0] | 0x01) % chrBankCount;
            chrBanks[6] = (bankRegs[1] & 0xFE) % chrBankCount;
            chrBanks[7] = (bankRegs[1] | 0x01) % chrBankCount;
            chrBanks[0] = bankRegs[2] % chrBankCount;
            chrBanks[1] = bankRegs[3] % chrBankCount;
            chrBanks[2] = bankRegs[4] % chrBankCount;
            chrBanks[3] = bankRegs[5] % chrBankCount;
        }
    }

    public override byte PpuRead(ushort address)
    {
        if (address < 0x2000)
        {
            int bank = address / 0x0400;
            int offset = address % 0x0400;
            return chrRom[(chrBanks[bank] * 0x0400) + offset];
        }

        return 0;
    }

    public override void PpuWrite(ushort address, byte value)
    {
    }
}
