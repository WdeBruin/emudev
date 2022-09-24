﻿using System.Collections;
using System.Diagnostics;

namespace MauiEmu;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Task.Run(() => DrawLoop());
        Task.Run(() => InitChip8());
    }    

    private async Task DrawLoop()
    {
        while (true)
        {
            try
            {
                var elapsed = Stopwatch.StartNew();
                await Draw();
                elapsed.Stop();
                Thread.Sleep(16 - (int)elapsed.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {

            }
        }
    }

    // Speed
    private const int ips = 700; // Instructions per second

    // Components
    private byte[] _memory; // 4kb
    private BitArray _display; // 64*32px
    private (byte msb, byte lsb)[] _stack; // 16 2bit entries
    private byte[] _regIndex = new byte[2]; // 16bit index reg
    private byte _regDelayTimer; // if above 0, decrease by 1 at 60hz
    private byte _regSoundTimer; // beeps, works like delaytimer
    private byte[] _regV; // V0 - VF registers, general purpose variable registers

    private int _pc; // Program Counter

    private async Task InitChip8()
    {
        // Set ram to 4kb
        _memory = new byte[4096];

        // Set font in ram        
        byte[] font = new byte[80]
        {
            0xF0, 0x90, 0x90, 0x90, 0xF0, // 0
            0x20, 0x60, 0x20, 0x20, 0x70, // 1
            0xF0, 0x10, 0xF0, 0x80, 0xF0, // 2
            0xF0, 0x10, 0xF0, 0x10, 0xF0, // 3
            0x90, 0x90, 0xF0, 0x10, 0x10, // 4
            0xF0, 0x80, 0xF0, 0x10, 0xF0, // 5
            0xF0, 0x80, 0xF0, 0x90, 0xF0, // 6
            0xF0, 0x10, 0x20, 0x40, 0x40, // 7
            0xF0, 0x90, 0xF0, 0x90, 0xF0, // 8
            0xF0, 0x90, 0xF0, 0x10, 0xF0, // 9
            0xF0, 0x90, 0xF0, 0x90, 0x90, // A
            0xE0, 0x90, 0xE0, 0x90, 0xE0, // B
            0xF0, 0x80, 0x80, 0x80, 0xF0, // C
            0xE0, 0x90, 0x90, 0x90, 0xE0, // D
            0xF0, 0x80, 0xF0, 0x80, 0xF0, // E
            0xF0, 0x80, 0xF0, 0x80, 0x80  // F
        };
        Buffer.BlockCopy(font, 0x00, _memory, 0x50, 80);

        // Set display
        _display = new BitArray(new byte[256]); // 64 px wide, 32 px tall. 8 byte wide * 32 = 256 byte.

        // Init stack to 16 two byte entries
        _stack = new (byte msb, byte lsb)[16];

        _regDelayTimer = new byte();
        _regSoundTimer = new byte();
        _regV = new byte[16];

        // Load program at 0x200
        try
        {
            var rom = await FileSystem.OpenAppPackageFileAsync("IBM logo.ch8");
            var ms = new MemoryStream();
            rom.CopyTo(ms);
            var romArray = ms.ToArray();
            Buffer.BlockCopy(romArray, 0, _memory, 0x200, romArray.Length);

            // Set program counter to start of program and run
            _pc = 0x200;
            await StartLoop();
        }
        catch (Exception ex)
        {

            throw;
        }        
    }

    private async Task StartLoop()
    {
        Stopwatch t = new Stopwatch();
        float stepTime = 1000 / ips;

        while (true)
        {
            t.Reset();
            t.Start();

            // Fetch            
            (byte msb, byte lsb) instruction = (_memory[_pc], _memory[_pc + 1]); // Fetch instruction from memory at current program counter (PC)
            _pc += 2; // Increment program counter by 2 bytes

            // Decode and execute
            // Decode instruction to find out what emulator should do
            int C = instruction.msb >> 4; // First nible, category of instruction
            int X = instruction.msb << 4; // Second nible, used to look up 1 of 16 registers V0-VF
            int Y = instruction.lsb >> 4; // Third nibble, used to look up 1 of 16 registers V0-VF
            int N = instruction.lsb << 4; // Fourth nibble, 4 bit number (0-F)
            int NN = instruction.lsb; // Second byte, 8 bit immediate number
            int NNN = instruction.msb << 4 | instruction.lsb; // 12 bit immediate memory address

            switch (C)
            {
                case 0x0:
                    if (X == 0x0 && Y == 0xE && N == 0x0) // 00E0 => clear screen
                    {
                        // Skiasharp clear screen
                    }
                    break;
                case 0x1:
                    // 1NNN => jump to the memory address
                    _pc = NNN;
                    break;
                case 0x2:
                    break;
                case 0x3:
                    break;
                case 0x4:
                    break;
                case 0x5:
                    break;
                case 0x6:
                    // 6XNN => set register VX
                    _regV[X] = (byte)NN;
                    break;
                case 0x7:
                    // 7XNN => add value to register VX
                    _regV[X] = (byte)(_regV[X] + NN);
                    break;
                case 0x8:
                    break;
                case 0x9:
                    break;
                case 0xA:
                    // ANNN => set index register I
                    Buffer.BlockCopy(BitConverter.GetBytes(NNN), 0, _regIndex, 0, 2);
                    break;
                case 0xB:
                    break;
                case 0xC:
                    break;
                case 0xD:
                    // DXYN => display/draw.
                    // Position is register X Y, sprite is at index register I, N pixels tall 
                    int x = _regV[X] % 64; // wrap
                    int y = _regV[Y] % 32; // wrap
                    _regV[0xF] = 0x0;

                    for (int i = 0; i < N; i++)
                    {
                        BitArray spriteData = new BitArray(_memory[_regIndex[0] | _regIndex[1]] + i);
                        for (int j = 0; j < spriteData.Length; j++)
                        {
                            bool pixelIsOn = _display[y * 8 + x];
                            if (spriteData[j] == true)
                            {
                                if (pixelIsOn)
                                {
                                    _display[y * 8 + x] = false;
                                    _regV[0xF] = 0x1;
                                    Display.SetPixel(x, y, false);                                    
                                }
                                else
                                {
                                    _display[y * 8 + x] = true;
                                    Display.SetPixel(x, y, true);
                                }
                            }
                            if (x == 63) break;
                            x++;
                        }
                        if (y == 31) break;
                        y++;
                    }
                    break;
                case 0xE:
                    break;
                case 0xF:
                    break;
                default:
                    break;
            }

            t.Stop();
            int timeLeft = Math.Max(0, (int)stepTime - (int)t.ElapsedMilliseconds);
            await Task.Delay(timeLeft);
        }
    }

    async Task Draw()
    {
        //MainThread.BeginInvokeOnMainThread(() => gView.Invalidate());
    }
}

public class GraphicsDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Green;

        for (int y = 0; y < Display.Pixels.GetLength(1); y++)
        {
            for (int x = 0; x < Display.Pixels.GetLength(0); x++)
            {
                var px = Display.Pixels[x, y];
                if (px == true)
                {
                    canvas.FillRectangle(x * Display.PixelSize, y * Display.PixelSize, Display.PixelSize, Display.PixelSize);
                }
            }
        }       
    }
}

public enum GameState
{
    Started,
    Stopped
}


public static class Display
{
    public static int PixelSize = 10; // X times 64 * 32 resolution
    public static bool[,] Pixels { get; set; } = new bool[64, 32];

    internal static void SetPixel(int x, int y, bool v)
    {
        Pixels[x, y] = v;
    }
}