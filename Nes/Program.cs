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
var apu = new Apu();
var controller1 = new Controller();
var controller2 = new Controller();
var memory = new Memory(bytes, ppu, apu, controller1, controller2);
apu.Memory = memory;
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
        apu.Step();

        if (ppu.FrameReady)
        {
            frames++;
            var elapsed = sw.ElapsedMilliseconds;
            double expected = frames * (1000.0 / 60.0);
            double speed = expected > 0 ? (expected / Math.Max(elapsed, 1)) : 1.0;
            window.EmulationSpeed = speed;

            if (elapsed < expected)
                Thread.Sleep((int)(expected - elapsed));
        }

        if (ppu.NmiPending)
        {
            ppu.NmiPending = false;
            cpu.TriggerNmi();
        }
    }
});

window.Run();
apu.Dispose();
