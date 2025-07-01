using System.Diagnostics;
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
var controller1 = new Controller();
var controller2 = new Controller();
var memory = new Memory(bytes, ppu, controller1, controller2);
ppu.Mapper = memory.Mapper;
ppu.ChrRom = memory.ChrRom;
var cpu = new Cpu(memory);
//cpu.PrintLog = true;
cpu.Reset(/* 0xC000 */);

Console.WriteLine("Emulation started");

var window = new NesWindow(ppu, controller1, controller2);

var cpuThread = Task.Run(() =>
{
    var sw = Stopwatch.StartNew();
    int frames = 0;

    while (true)
    {
        cpu.Step();
        for (int i = 0; i < 3; i++)
            ppu.Step();

        if (ppu.FrameReady)
        {
            frames++;
            var elapsed = sw.ElapsedMilliseconds;
            if (elapsed < frames * (1000 / 60))
                Thread.Sleep((int)(frames * (1000 / 60) - elapsed));
        }

        if (ppu.NmiPending)
        {
            ppu.NmiPending = false;
            cpu.TriggerNmi();
        }
    }
});

window.Run();
