using MauiEmu.Emulator.Chip8;

namespace MauiEmu;

public partial class MainPage : ContentPage
{
    private Chip8 _chip8;

    public MainPage()
    {
        InitializeComponent(); 
        
        _chip8 = new Chip8();
        MessagingCenter.Subscribe<Chip8>(this, "draw", (sender) =>
        {
            MainThread.InvokeOnMainThreadAsync(() => gView.Invalidate());           
        });

        Task.Run(() => Load());
    }

    /// <summary>
    /// Load selected ROM and start chip8
    /// </summary>
    /// <returns></returns>
    private async Task Load()
    {   
        // Load program at 0x200
        try
        {
            var rom = await FileSystem.OpenAppPackageFileAsync("tetris.ch8");
            var ms = new MemoryStream();
            rom.CopyTo(ms);
            var romArray = ms.ToArray();
            
            await _chip8.LoadRomAndStart(romArray);
        }
        catch (Exception ex)
        {
            
        }        
    }    

    string DebugInt(int v)
    {
        return $"{Convert.ToString(v, 16)}";
    }

    private void Key_Released(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = null;
    }

    private void Key1_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x1';
    }

    private void Key2_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x2';
    }

    private void Key3_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x3';
    }

    private void KeyC_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xC';
    }

    private void Key4_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x4';
    }

    private void Key5_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x5';
    }

    private void Key6_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x6';
    }

    private void KeyD_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xD';
    }

    private void Key7_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x7';
    }

    private void Key8_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x8';
    }

    private void Key9_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x9';
    }

    private void KeyE_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xE';
    }

    private void KeyA_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xA';
    }

    private void Key0_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\x0';
    }

    private void KeyB_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xB';
    }

    private void KeyF_Pressed(object sender, EventArgs e)
    {
        _chip8.CurrentKeyPressed = '\xF';
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
                    canvas.FillColor = new Color(0, 0, 0);
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
