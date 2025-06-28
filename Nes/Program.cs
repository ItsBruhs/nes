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

var memory = new Memory(bytes);
var cpu = new Cpu(memory);
cpu.PrintLog = true;
cpu.Reset(/* 0xC000 */);

Console.WriteLine("Emulation started");

try
{
    while (true)
        cpu.Step();
}
finally
{
    File.WriteAllText("./instruction_log.txt", cpu.InstructionLog);
}
