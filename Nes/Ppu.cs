namespace Nes;

public class Ppu
{
    public uint[] Framebuffer = new uint[256 * 240]; // RGBA8888
    public bool FrameReady = false;

    public byte[] Vram = new byte[0x4000];

    public byte Control;
    public bool NmiPending = false;

    private int cycle = 0;
    private int scanline = 0;

    private ushort v; // current vram address
    private ushort t; // temp vram address
    private byte x; // fine x scroll
    private bool w; // write toggle (false = first write)

    public byte[] ChrRom = [];

    public byte[] PaletteRam = new byte[32];

    public static readonly uint[] NesBasePalette =
    [
        0x545454FF, 0x001E74FF, 0x081090FF, 0x300088FF, 0x440064FF, 0x5C0030FF, 0x540400FF, 0x3C1800FF,
        0x202A00FF, 0x083A00FF, 0x004000FF, 0x003C28FF, 0x002840FF, 0x000000FF, 0x000000FF, 0x000000FF,
        0x989698FF, 0x084CC4FF, 0x3032ECFF, 0x5C1EE4FF, 0x8814B0FF, 0xA01464FF, 0x982220FF, 0x783C00FF,
        0x545A00FF, 0x287200FF, 0x087C00FF, 0x007628FF, 0x006678FF, 0x000000FF, 0x000000FF, 0x000000FF,
        0xECEEEAFF, 0x4C9AECFF, 0x787CECFF, 0xB062ECFF, 0xE454ECFF, 0xEC58B4FF, 0xEC6A64FF, 0xD48820FF,
        0xA0AA00FF, 0x74C400FF, 0x4CD020FF, 0x38CC6CFF, 0x38B4CCFF, 0x3C3C3CFF, 0x000000FF, 0x000000FF,
        0xFCFCFCFF, 0xA8CCFCFF, 0xBCBCFCFF, 0xD4B2FCFF, 0xECA8FCFF, 0xF4ACD4FF, 0xF4B4B0FF, 0xF4C490FF,
        0xECDC90FF, 0xD4E48CFF, 0xBCECACFF, 0xAEECCEFF, 0xAEECECFF, 0xAEAEAEFF, 0x000000FF, 0x000000FF
    ];

    private int read2002Counter = 0;

    public void Step()
    {
        cycle++;

        if (cycle >= 341)
        {
            cycle = 0;
            scanline++;

            if (scanline == 241)
            {
                // Enter VBlank and trigger NMI
                if ((Control & 0x80) != 0)
                    NmiPending = true;
            }

            if (scanline >= 262)
            {
                scanline = 0;
                NmiPending = false;

                RenderBackground();
                FrameReady = true;
            }
        }
    }

    public void RenderBackground()
    {
        for (int row = 0; row < 30; row++)
        {
            for (int col = 0; col < 32; col++)
            {
                int tileIndex = Vram[0x2000 + row * 32 + col]; // nametable entry
                byte[,] tile = DecodeTile(tileIndex);

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        byte pixelValue = tile[y, x]; // 0-3

                        // Look up palette index from $3F00-$3F03
                        byte paletteEntry = PaletteRam[0x00 + pixelValue]; // background palette 0
                        uint color = NesBasePalette[paletteEntry]; // RGBA

                        int px = col * 8 + x;
                        int py = row * 8 + y;

                        Framebuffer[py * 256 + px] = color;
                    }
                }
            }
        }

        FrameReady = true;
    }

    public byte[,] DecodeTile(int index)
    {
        byte[,] pixels = new byte[8, 8];
        int addr = index * 16;

        for (int y = 0; y < 8; y++)
        {
            byte low = ChrRom[addr + y];
            byte high = ChrRom[addr + y + 8];

            for (int x = 0; x < 8; x++)
            {
                int bit = 7 - x;
                byte bit0 = (byte)((low >> bit) & 1);
                byte bit1 = (byte)((high >> bit) & 1);
                pixels[y, x] = (byte)((bit1 << 1) | bit0); // 0-3
            }
        }

        return pixels;
    }

    public byte[,] GetTile(int index)
    {
        byte[,] pixels = new byte[8, 8];

        int baseAddr = index * 16;

        for (int y = 0; y < 8; y++)
        {
            byte low = ChrRom[baseAddr + y];
            byte high = ChrRom[baseAddr + y + 8];

            for (int x = 0; x < 8; x++)
            {
                int bit = 7 - x;
                byte bit0 = (byte)((low >> bit) & 1);
                byte bit1 = (byte)((high >> bit) & 1);
                pixels[y, x] = (byte)((bit1 << 1) | bit0);
            }
        }

        return pixels;
    }

    public byte ReadRegister(ushort reg)
    {
        switch (reg)
        {
            case 2: // Fake PPUSTATUS
                read2002Counter++;

                if (read2002Counter > 5)
                    return 0x80;
                else
                    return 0x00;

            case 7:
                byte result;
                if (v >= 0x3F00)
                    result = PaletteRam[v % 32];
                else
                    result = Vram[v];

                v += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                return result;
        }

        return 0;
    }

    public void WriteRegister(ushort reg, byte value)
    {
        //Console.WriteLine($"Ppu write @ {reg:X4} = {value}");

        switch (reg)
        {
            case 0: // $2000 - Control
                Control = value;
                t = (ushort)((t & 0xF3FF) | ((value & 0b11) << 10)); // bits 0-1 -> nametable select
                break;

            case 5: // $2005 - Scroll
                if (!w)
                {
                    x = (byte)(value & 0b111); // fine x scroll
                    t = (ushort)((t & 0xFFE0) | (value >> 3)); // coarse x
                    w = true;
                }
                else
                {
                    t = (ushort)((t & 0x8FFF) | ((value & 0b111) << 12)); // fine y
                    t = (ushort)((t & 0xFC1F) | ((value & 0xF8) << 2)); // coarse y
                    w = false;
                }

                break;

            case 6: // $2006 - set address
                //Console.WriteLine($"$2006 write: {(w ? "low" : "high")} = {value:X2}");
                if (!w)
                {
                    t = (ushort)((t & 0x00FF) | ((value & 0x3F) << 8));
                    w = true;
                }
                else
                {
                    t = (ushort)((t & 0xFF00) | value);
                    v = t;
                    w = false;
                }
                break;

            case 7: // $2007 - write data to v
                //Console.WriteLine($"$2007 write to {v:X4} = {value:X2}");
                if (v >= 0x3F00 && v <= 0x3FFF)
                    PaletteRam[v % 32] = value;
                else
                    Vram[v % Vram.Length] = value;

                // Auto increment v by 1 or 32 depending on bit 2 of $2000
                v += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                break;
        }
    }
}
