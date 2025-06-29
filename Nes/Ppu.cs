namespace Nes;

public class Ppu(string palettePath)
{
    public uint[] Framebuffer = new uint[256 * 240]; // RGBA8888
    public bool FrameReady = false;

    public byte[] Vram = new byte[0x4000];

    public byte Control;
    public bool NmiPending = false;
    public bool VBlank = false;

    public int Cycle = 0;
    public int Scanline = 0;

    public ushort V; // current vram address
    public ushort T; // temp vram address
    public byte X; // fine x scroll
    public bool W; // write toggle (false = first write)

    public byte[] ChrRom = [];

    public byte[] PaletteRam = new byte[32];

    public uint[] NesBasePalette = LoadNesPalette(palettePath);

    private static uint[] LoadNesPalette(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        uint[] palette = new uint[64];
        for (int i = 0; i < 64; i++)
        {
            byte r = raw[i * 3];
            byte g = raw[i * 3 + 1];
            byte b = raw[i * 3 + 2];
            palette[i] = (uint)(0xFF << 24 | b << 16 | g << 8 | r);
        }

        return palette;
    }

    public void Step()
    {
        Cycle++;

        if (Cycle == 1)
        {
            if (Scanline == 241)
            {
                VBlank = true;

                if ((Control & 0x80) != 0)
                    NmiPending = true;
            }
            else if (Scanline == 261)
            {
                VBlank = false;
                NmiPending = false;
            }
        }

        if (Cycle >= 341)
        {
            Cycle = 0;
            Scanline++;

            if (Scanline >= 262)
            {
                Scanline = 0;

                RenderBackground();
                FrameReady = true;
            }
        }
    }

    public void RenderBackground()
    {
        int fineY = (V >> 12) & 0b111;
        int coarseY = (V >> 5) & 0x1F;
        int coarseXStart = V & 0b11111;
        int nametable = (V >> 10) & 0b11;

        for (int py = 0; py < 240; py++)
        {
            int yTile = (coarseY + (py + fineY) / 8) & 0x1F;
            int fineYInTile = (py + fineY) % 8;

            for (int px = 0; px < 256; px++)
            {
                int xScroll = px + X;
                int xTile = (coarseXStart + xScroll / 8) & 0x1F;
                int fineXInTile = xScroll % 8;

                int nameTableX = (coarseXStart + xScroll / 8) >> 5;
                int nameTableY = (coarseY + (py + fineY) / 8) >> 5;
                int selectedNametable = (nametable + nameTableY * 2 + nameTableX) & 0b11;
                int nametableBase = 0x2000 + selectedNametable * 0x400;

                int tileIndex = Vram[nametableBase + yTile * 32 + xTile];
                byte[,] tile = GetTile(tileIndex);

                int attributeAddr = nametableBase + 0x3C0 + (yTile / 4) * 8 + (xTile / 4);
                byte attributeByte = Vram[attributeAddr];
                int shift = ((yTile % 4) / 2) * 4 + ((xTile % 4) / 2) * 2;
                int paletteIndex = (attributeByte >> shift) & 0b11;

                byte pixelValue = tile[fineYInTile, fineXInTile];
                byte paletteEntry = pixelValue == 0
                    ? PaletteRam[0]
                    : PaletteRam[(paletteIndex * 4 + pixelValue) & 0x1F];

                if ((paletteEntry & 0x13) == 0x10)
                    paletteEntry &= 0x0F;

                if (paletteEntry >= NesBasePalette.Length)
                    paletteEntry = 0;

                uint color = NesBasePalette[paletteEntry];
                Framebuffer[py * 256 + px] = color;
            }
        }

        FrameReady = true;
    }

    public byte[,] GetTile(int index)
    {
        byte[,] pixels = new byte[8, 8];

        int baseAddr = index * 16;

        for (int y = 0; y < 8; y++)
        {
            var tileSource = ChrRom.Length > 0 ? ChrRom : Vram;
            byte low = tileSource[baseAddr + y];
            byte high = tileSource[baseAddr + y + 8];

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
            case 2: // $2002 - PPUSTATUS
                byte status = 0;
                if (VBlank) status |= 0x80;
                W = false;
                VBlank = false;
                return status;

            case 7:
                byte result;
                int addr = V & 0x3FFF;

                if (addr >= 0x3F00 && addr < 0x4000)
                {
                    int paletteAddr = addr & 0x1F;
                    if ((paletteAddr & 0x13) == 0x10)
                        paletteAddr &= 0x0F;
                    result = PaletteRam[paletteAddr];
                }
                else
                {
                    result = Vram[addr];
                }

                V += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                return result;
        }

        return 0;
    }

    public void WriteRegister(ushort reg, byte value)
    {
        switch (reg)
        {
            case 0: // $2000
                Control = value;
                T = (ushort)((T & 0xF3FF) | ((value & 0b11) << 10));
                break;

            case 5: // $2005 - Scroll
                if (!W)
                {
                    X = (byte)(value & 0b111);
                    T = (ushort)((T & 0xFFE0) | (value >> 3));
                    W = true;
                }
                else
                {
                    T = (ushort)((T & 0x8FFF) | ((value & 0b111) << 12));
                    T = (ushort)((T & 0xFC1F) | ((value & 0xF8) << 2));
                    W = false;
                }
                break;

            case 6: // $2006 - set address
                if (!W)
                {
                    T = (ushort)((T & 0x00FF) | ((value & 0x3F) << 8));
                    W = true;
                }
                else
                {
                    T = (ushort)((T & 0xFF00) | value);
                    V = T;
                    W = false;
                }
                break;

            case 7:
                V &= 0x3FFF;
                int addr = V & 0x3FFF;

                if (addr >= 0x3F00 && addr < 0x4000)
                {
                    int paletteAddr = addr & 0x1F;
                    if ((paletteAddr & 0x13) == 0x10)
                        paletteAddr &= 0x0F;
                    PaletteRam[paletteAddr] = value;
                }
                else
                {
                    Vram[addr % Vram.Length] = value;
                }

                V += (ushort)((Control & 0x04) != 0 ? 32 : 1);
                break;
        }
    }
}
