using System.Collections;
using System.Diagnostics;
using System.Windows.Input;

namespace MauiEmu;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Task.Run(() => InitChip8());
    }

    // Speed
    private const int ips = 700; // Instructions per second

    // Components
    private byte[] _memory; // 4kb    
    Stack<ushort> _stack; 
    private ushort _regIndex = 0; // 16bit index reg
    private byte _regDelayTimer; // if above 0, decrease by 1 at 60hz
    private byte _regSoundTimer; // beeps, works like delaytimer
    private byte[] _regV; // V0 - VF registers, general purpose variable registers
    private char _keyPressed; // If key is pressed its registered here

    private ushort _pc; // Program Counter

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
        
        // Init stack to 16 two byte entries
        _stack = new Stack<ushort>(16);

        _regDelayTimer = new byte();
        _regSoundTimer = new byte();
        _regV = new byte[16];

        // Load program at 0x200
        try
        {
            //var rom = await FileSystem.OpenAppPackageFileAsync("test_opcode.ch8");
            var rom = await FileSystem.OpenAppPackageFileAsync("chiptest.ch8");
            //var rom = await FileSystem.OpenAppPackageFileAsync("ibm.ch8");
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
            
        }        
    }

    private async Task StartLoop()
    {
        Stopwatch t = new Stopwatch();
        float stepTime = (float)1000 / ips;

        while (true)
        {
            t.Reset();
            t.Start();

            // Fetch            
            (byte msb, byte lsb) instruction = (_memory[_pc], _memory[_pc + 1]); // Fetch instruction from memory at current program counter (PC)
            //Debug.WriteLine($"{_pc}: {DebugInt(instruction.msb)}{DebugInt(instruction.lsb)}");
            _pc += 2; // Increment program counter by 2 bytes

            // Decode and execute
            // Decode instruction to find out what emulator should do
            byte C = (byte)(instruction.msb >> 4 & 0xF); // First nible, category of instruction
            byte X = (byte)(instruction.msb & 0xF); // Second nible, used to look up 1 of 16 registers V0-VF
            byte Y = (byte)(instruction.lsb >> 4 & 0xF); // Third nibble, used to look up 1 of 16 registers V0-VF
            byte N = (byte)(instruction.lsb & 0xF); // Fourth nibble, 4 bit number (0-F)
            byte NN = instruction.lsb; // Second byte, 8 bit immediate number
            ushort NNN = (ushort)((ushort)(X << 8) | ((ushort)(Y << 4)) | ((ushort)N)); // 12 bit immediate memory address. Does not fit in a byte, go for 32 bit int.            
            
            switch (C)
            {
                case 0x0:                    
                    if (instruction.msb == 0x00 && instruction.lsb == 0xe0) // 00E0 => clear screen
                    {
                        Display.Pixels = new bool[64, 32];
                        await Draw();
                    }
                    if (instruction.msb == 0x00 && instruction.lsb == 0xee) // 00EE => pop
                    {
                        _pc = _stack.Pop();
                    }
                    break;
                case 0x1:
                    // 1NNN => jump to the memory address
                    _pc = NNN;
                    break;
                case 0x2:
                    // 2NNN => call memory address (first push to stack pc)
                    _stack.Push(_pc);
                    _pc = NNN;
                    break;
                case 0x3:
                    // 3XNN => SKip 1 instruction if VX == NN
                    if (_regV[X] == NN) _pc += 2;
                    break;
                case 0x4:
                    // 4XNN => SKip 1 instruction if VX != NN
                    if (_regV[X] != NN) _pc += 2;
                    break;
                case 0x5:
                    // 5XY0 => Skip 1 instruction if VX == VY
                    if (_regV[X] == _regV[Y]) _pc += 2;
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
                    switch (N)
                    {
                        case 0x0: // 8XY0 => Set
                            _regV[X] = _regV[Y];
                            break;
                        case 0x1: // 8XY1 => Binary OR
                            _regV[X] = (byte)(_regV[X] | _regV[Y]);
                            break;
                        case 0x2: // 8XY2 => Binary AND
                            _regV[X] = (byte)(_regV[X] & _regV[Y]);
                            break;
                        case 0x3: // 8XY3 => Logical XOR
                            _regV[X] = (byte)(_regV[X] ^ _regV[Y]);
                            break;
                        case 0x4: // 8XY4 => Add With carry flag on overflow
                            if (_regV[X] + _regV[Y] > 255) _regV[0xF] = 0x1;
                            _regV[X] = (byte)(_regV[X] + _regV[Y]);
                            break;
                        case 0x5: // 8XY5 => Subtract VX - VY
                            _regV[X] = (byte)(_regV[X] - _regV[Y]);
                            break;
                        case 0x7: // 8XY7 => Subtract VY - VX
                            _regV[X] = (byte)(_regV[Y] - _regV[X]);
                            break;
                        case 0x6: // 8XY6 => Shift (ambigious)
                            //_regV[X] = _regV[Y]; // optional, old implementation                            
                            _regV[0xF] = (byte)(_regV[X] & 1);
                            _regV[X] = (byte)(_regV[X] >> 1);
                            break;
                        case 0xE: // 8XYE => Shift (ambigious)
                            //_regV[X] = _regV[Y]; // optional, old implementation
                            _regV[0xF] = (byte)((_regV[X] >> 7) & 1);
                            _regV[X] = (byte)(_regV[X] << 1);
                            break;
                        default:
                            break;
                    }
                    break;
                case 0x9:
                    // 9XY0 => Skip 1 instruction if VX != VY
                    if (_regV[X] != _regV[Y]) _pc += 2;
                    break;
                case 0xA:
                    // ANNN => set index register I                    
                    _regIndex = NNN;
                    break;
                case 0xB: // BNNN of BXNN, ambigious. BNNN implemented. Jump to NNN + regV[0]
                    _pc = (ushort)(NNN + _regV[0]);
                    break;
                case 0xC:
                    _regV[X] = (byte)(new Random().Next(NN) & NN);
                    break;
                case 0xD:
                    // DXYN => display/draw.
                    // Position is register X Y, sprite is at index register I, N pixels tall 
                    int xStart = _regV[X] % 64; // wrap
                    int yStart = _regV[Y] % 32; // wrap
                    _regV[0xF] = 0x0;

                    for (int yd = 0; yd < N; yd++)
                    {                        
                        byte spriteByte = _memory[_regIndex + yd]; // byte of the sprite for this row                        
                        
                        for (int xd = 0; xd < 8; xd++)
                        {
                            bool pixelIsOn = Display.Pixels[xStart + xd, yStart + yd];
                            if (((spriteByte >> 7-xd) & 1) == 1) // reverted draw, start with leas significant bit
                            {
                                if (pixelIsOn)
                                {                                    
                                    _regV[0xF] = 0x1;
                                    Display.SetPixel(xStart + xd, yStart + yd, false);
                                    await Draw();
                                }
                                else
                                {                                    
                                    Display.SetPixel(xStart + xd, yStart + yd, true);
                                    await Draw();
                                }
                            }
                            if (xStart+xd == 63) break;
                        }
                        if (yStart+yd == 31) break;
                    }
                    break;
                case 0xE:
                    if (instruction.lsb == 0x9E) // EX9E => skip instruction if key pressed == value in VX
                    {
                        if (_keyPressed == _regV[X])
                        {
                            _pc += 2;
                        }
                    }

                    if (instruction.lsb == 0xA1) // EXA1 => skip instruction if key pressed != value in VX
                    {
                        if (_keyPressed != _regV[X])
                        {
                            _pc += 2;
                        }
                    }
                    break;
                case 0xF:
                    break;
                default:
                    break;
            }

            t.Stop();
            float timeLeft = Math.Max(0, (float)stepTime - t.ElapsedMilliseconds);
            await Task.Delay((int)timeLeft);
        }
    }

    async Task Draw()
    {        
        await Dispatcher.DispatchAsync(() => gView.Invalidate());
        //await Task.Delay(100); // for debug only!
        //MainThread.BeginInvokeOnMainThread(() => gView.Invalidate());
    }

    string DebugInt(int v)
    {
        return $"{Convert.ToString(v, 16)}";
    }

    private void Key_Released(object sender, EventArgs e)
    {
        _keyPressed = '\0';
    }

    private void Key1_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x1';
    }

    private void Key2_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x2';
    }

    private void Key3_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x3';
    }

    private void KeyC_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xC';
    }

    private void Key4_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x4';
    }

    private void Key5_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x5';
    }

    private void Key6_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x6';
    }

    private void KeyD_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xD';
    }

    private void Key7_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x7';
    }

    private void Key8_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x8';
    }

    private void Key9_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x9';
    }

    private void KeyE_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xE';
    }

    private void KeyA_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xA';
    }

    private void Key0_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\x0';
    }

    private void KeyB_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xB';
    }

    private void KeyF_Pressed(object sender, EventArgs e)
    {
        _keyPressed = '\xF';
    }
}

public class GraphicsDrawable : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        for (int y = 0; y < Display.Pixels.GetLength(1); y++)
        {
            for (int x = 0; x < Display.Pixels.GetLength(0); x++)
            {
                var px = Display.Pixels[x, y];
                if (px == true)
                {
                    canvas.FillColor = new Color(2, 91, 24);
                }
                else
                {
                    //canvas.FillColor = new Color(104, 104, 104);
                    canvas.FillColor = new Color(255, 255, 255);
                }
                canvas.FillRectangle(x * Display.PixelSize, y * Display.PixelSize, Display.PixelSize, Display.PixelSize);
            }
        }       
    }
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