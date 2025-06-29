namespace Nes;

public class Controller
{
    private byte shiftRegister = 0;
    private byte buttons = 0;
    private bool strobe = false;

    public void Write(byte value)
    {
        strobe = (value & 1) != 0;
        if (strobe)
            shiftRegister = buttons;
    }

    public byte Read()
    {
        byte result = (byte)(shiftRegister & 1);
        if (!strobe)
            shiftRegister >>= 1;
        return result;
    }

    public void SetButtons(bool a, bool b, bool select, bool start, bool up, bool down, bool left, bool right)
    {
        buttons = 0;
        if (a) buttons |= 1 << 0;
        if (b) buttons |= 1 << 1;
        if (select) buttons |= 1 << 2;
        if (start) buttons |= 1 << 3;
        if (up) buttons |= 1 << 4;
        if (down) buttons |= 1 << 5;
        if (left) buttons |= 1 << 6;
        if (right) buttons |= 1 << 7;
    }
}
