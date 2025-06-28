using Nes;

var cmdArgs = Environment.GetCommandLineArgs();

if (cmdArgs.Length < 2)
{
    Console.WriteLine("No input file provided");
    Console.ReadLine();
    return;
}

var filePath = cmdArgs[1];
var bytes = File.ReadAllBytes(filePath);

Console.WriteLine($"Loading file: {filePath}");

var ppu = new Ppu();
var memory = new Memory(bytes, ppu);
ppu.ChrRom = memory.ChrRom;
var cpu = new Cpu(memory);
//cpu.PrintLog = true;
cpu.Reset(/* 0xC000 */);

Console.WriteLine("Emulation started");

var window = new NesWindow(ppu);

var cpuThread = Task.Run(() =>
{
    while (true)
    {
        cpu.Step();

        for (int i = 0; i < 3; i++)
        {
            ppu.Step();

            if (ppu.NmiPending)
            {
                ppu.NmiPending = false;
                cpu.TriggerNmi();
            }
        }
    }
});

window.Run();
