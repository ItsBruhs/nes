using NAudio.Wave;

namespace Nes;

public class Apu : IDisposable
{
    public Memory? Memory; // DMC needs ram access

    private byte[] registers = new byte[0x18];

    private float cpuClock = 1789773;
    private int sampleRate = 44100;

    private WaveOutEvent output;
    private BufferedWaveProvider buffer;

    private Thread thread;
    private bool running = true;

    // Pulse 1
    private int timer1Low = 0;
    private int timer1High = 0;
    private int timer1 => (timer1High << 8) | timer1Low;
    private float duty1 = 0.125f;
    private int volume1 = 15;
    private bool pulse1Enabled = false;
    private int length1 = 0;

    // Pulse 2
    private int timer2Low = 0;
    private int timer2High = 0;
    private int timer2 => (timer2High << 8) | timer2Low;
    private float duty2 = 0.125f;
    private int volume2 = 15;
    private bool pulse2Enabled = false;
    private int length2 = 0;

    // Triangle
    private int triangleTimerLow = 0;
    private int triangleTimerHigh = 0;
    private int triangleTimer => (triangleTimerHigh << 8) | triangleTimerLow;

    private int triangleLengthCounter = 0;
    private int triangleLinearCounter = 0;
    private int triangleLinearReload = 0;
    private bool triangleControlFlag = false;
    private bool triangleReloadFlag = false;

    private int trianglePhase = 0;
    private int triangleCounter = 0;

    private static readonly int[] lengthTable =
    [
        10, 254, 20,  2, 40,  4, 80,  6,
        160, 8, 60, 10, 14, 12, 26, 14,
        12, 16, 24, 18, 48, 20, 96, 22,
        192, 24, 72, 26, 16, 28, 32, 30
    ];

    // Noise
    private int noiseTimerIndex = 0;
    private int noiseCounter = 0;
    private bool noiseMode = false;
    private int noiseLengthCounter = 0;
    private int noiseVolume = 15;

    private ushort noiseShiftRegister = 1;

    private static readonly int[] noisePeriods =
    [
        4, 8, 16, 32, 64, 96, 128, 160,
        202, 254, 380, 508, 762, 1016, 2034, 4068
    ];

    // DMC
    private byte dmcOutputLevel = 64;
    private int dmcTimer = 0;
    private int dmcTimerPeriod = 428;

    private int dmcAddress = 0xC000;
    private int dmcLength = 1;
    private int dmcCurrentLength = 0;
    private byte dmcSampleBuffer;
    private int dmcBitsLeft = 0;

    private static readonly int[] dmcRates =
    {
        428, 380, 340, 320, 286, 254, 226, 214,
        190, 160, 142, 128, 106,  84,  72,  54,
    };

    public Apu()
    {
        output = new WaveOutEvent();
        buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 1))
        {
            BufferDuration = TimeSpan.FromSeconds(1),
            DiscardOnBufferOverflow = true
        };

        output.Init(buffer);
        output.Play();

        thread = new Thread(AudioThread) { IsBackground = true };
        thread.Start();
    }

    public void Write(ushort address, byte value)
    {
        if (address < 0x4000 || address > 0x4017)
            return;

        registers[address - 0x4000] = value;

        switch (address)
        {
            // Pulse 1
            case 0x4000:
                duty1 = ((value >> 6) & 0b11) switch
                {
                    0b00 => 0.125f,
                    0b01 => 0.25f,
                    0b10 => 0.5f,
                    0b11 => 0.25f,
                    _ => 0.5f
                };
                volume1 = value & 0x0F;
                break;

            case 0x4002:
                timer1Low = value;
                break;

            case 0x4003:
                timer1High = value & 0b111;
                length1 = lengthTable[(value >> 3) & 0b11111];
                break;

            // Pulse 2
            case 0x4004:
                duty2 = ((value >> 6) & 0b11) switch
                {
                    0b00 => 0.125f,
                    0b01 => 0.25f,
                    0b10 => 0.5f,
                    0b11 => 0.25f,
                    _ => 0.5f
                };
                volume2 = value & 0x0F;
                break;

            case 0x4006:
                timer2Low = value;
                break;

            case 0x4007:
                timer2High = value & 0b111;
                length2 = lengthTable[(value >> 3) & 0b11111];
                break;

            case 0x4015:
                pulse1Enabled = (value & 0x01) != 0;
                pulse2Enabled = (value & 0x02) != 0;
                if (!pulse1Enabled) length1 = 0;
                if (!pulse2Enabled) length2 = 0;
                break;

            // Triangle
            case 0x4008:
                triangleControlFlag = (value & 0x80) != 0;
                triangleLinearReload = value & 0x7F;
                break;

            case 0x400A:
                triangleTimerLow = value;
                break;

            case 0x400B:
                triangleTimerHigh = value & 0x07;
                triangleLengthCounter = lengthTable[(value >> 3) & 0b11111];
                triangleReloadFlag = true;
                break;

            // Noise
            case 0x400C:
                noiseVolume = value & 0x0F;
                break;

            case 0x400E:
                noiseMode = (value & 0x80) != 0;
                noiseTimerIndex = value & 0x0F;
                break;

            case 0x400F:
                noiseLengthCounter = lengthTable[(value >> 3) & 0b11111];
                break;

            // DMC
            case 0x4010:
                int rateIndex = value & 0x0F;
                dmcTimerPeriod = dmcRates[rateIndex];
                break;

            case 0x4011:
                dmcOutputLevel = (byte)(value & 0x7F);
                break;

            case 0x4012:
                dmcAddress = 0x8000 + (value * 64);
                break;

            case 0x4013:
                dmcLength = (value * 16) + 1;
                dmcCurrentLength = dmcLength;
                break;
        }
    }

    public void Step()
    {
        if (triangleReloadFlag)
        {
            triangleLinearCounter = triangleLinearReload;
        }
        else if (triangleLinearCounter > 0)
        {
            triangleLinearCounter--;
        }

        if (!triangleControlFlag)
            triangleReloadFlag = false;
    }

    private void StepTriangle()
    {
        if (triangleLengthCounter > 0 && triangleLinearCounter > 0)
        {
            triangleCounter--;
            if (triangleCounter <= 0)
            {
                triangleCounter = triangleTimer + 1;
                trianglePhase = (trianglePhase + 1) % 32;
            }
        }
    }

    private float GetTriangleSample()
    {
        int value = trianglePhase < 16
            ? 15 - trianglePhase
            : trianglePhase - 16;

        return (value - 7.5f) / 7.5f;
    }

    private void StepNoise()
    {
        noiseCounter--;
        if (noiseCounter <= 0)
        {
            noiseCounter = noisePeriods[noiseTimerIndex];

            bool bit0 = (noiseShiftRegister & 0x1) != 0;
            bool bit1 = noiseMode
                ? (noiseShiftRegister & 0x40) != 0
                : (noiseShiftRegister & 0x2) != 0;

            bool feedback = bit0 ^ bit1;

            noiseShiftRegister >>= 1;
            if (feedback)
                noiseShiftRegister |= 0x4000;
        }
    }

    private float GetNoiseSample()
    {
        if ((noiseShiftRegister & 0x1) == 0)
            return noiseVolume / 15f;
        else
            return (-noiseVolume) / 15f;
    }

    private void StepDmc(byte[] cpuMemory)
    {
        dmcTimer--;
        if (dmcTimer <= 0)
        {
            dmcTimer = dmcTimerPeriod;

            if (dmcBitsLeft == 0 && dmcCurrentLength > 0)
            {
                dmcSampleBuffer = cpuMemory[dmcAddress++ % cpuMemory.Length];
                dmcBitsLeft = 8;
                dmcCurrentLength--;
            }

            if (dmcBitsLeft > 0)
            {
                bool bit = (dmcSampleBuffer & 0x01) != 0;

                if (bit && dmcOutputLevel <= 125)
                    dmcOutputLevel += 2;
                else if (!bit && dmcOutputLevel >= 2)
                    dmcOutputLevel -= 2;

                dmcSampleBuffer >>= 1;
                dmcBitsLeft--;
            }
        }
    }

    private void AudioThread()
    {
        const int samplesPerBuffer = 1024;
        byte[] samples = new byte[samplesPerBuffer * 2];

        while (running)
        {
            for (int i = 0; i < samplesPerBuffer; i++)
            {
                StepTriangle();
                StepNoise();
                if (Memory != null)
                    StepDmc(Memory.Ram);

                float sample = 0;

                if (length1 > 0 && pulse1Enabled)
                {
                    float freq1 = cpuClock / (16f * (timer1 + 1));
                    if (freq1 >= 20 && freq1 <= 20000)
                    {
                        int samplesPerCycle = (int)(sampleRate / freq1);
                        int dutySamples = (int)(samplesPerCycle * duty1);
                        int pos = i % samplesPerCycle;
                        sample = (pos < dutySamples ? 1 : -1) * (volume1 / 15f);
                    }
                }

                if (length2 > 0 && pulse2Enabled)
                {
                    float freq2 = cpuClock / (16f * (timer2 + 1));
                    if (freq2 >= 20 && freq2 <= 20000)
                    {
                        int samplesPerCycle = (int)(sampleRate / freq2);
                        int dutySamples = (int)(samplesPerCycle * duty2);
                        int pos = i % samplesPerCycle;
                        sample = (pos < dutySamples ? 1 : -1) * (volume2 / 15f);
                    }
                }

                float triangle = 0;
                if (triangleLengthCounter > 0 && triangleLinearCounter > 0 && triangleTimer > 0)
                    triangle = GetTriangleSample();

                sample += triangle * 0.5f;

                float noise = 0;
                if (noiseLengthCounter > 0)
                    noise = GetNoiseSample();

                sample += noise * 0.3f;

                if (Memory != null)
                {
                    float dmc = dmcOutputLevel / 127f;
                    sample += (dmc - 0.5f) * 0.5f;
                }

                short value = (short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue);
                samples[i * 2] = (byte)(value & 0xFF);
                samples[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
            }

            buffer.AddSamples(samples, 0, samples.Length);
            Thread.Sleep(10);
        }
    }

    public void Dispose()
    {
        running = false;
        thread?.Join();
        output?.Stop();
        output?.Dispose();
    }
}
