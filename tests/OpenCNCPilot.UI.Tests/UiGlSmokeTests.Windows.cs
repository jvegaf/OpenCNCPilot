using System;
using System.Runtime.InteropServices;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class UiGlSmokeTests_Windows
{
    [Trait("Category", "UI-GL")]
    [Fact]
    public void Offscreen_WGL_Context_Can_Clear_Color_When_Available()
    {
        // Sólo aplica a Windows; si no es Windows, salir silenciosamente.
        if (!OperatingSystem.IsWindows())
            return;

        IntPtr hwnd = IntPtr.Zero;
        IntPtr hdc = IntPtr.Zero;
        IntPtr hglrc = IntPtr.Zero;

        try
        {
            IntPtr hInstance = Win32.GetModuleHandleW(null);
            // Crear ventana oculta usando clase predefinida "STATIC"
            hwnd = Win32.CreateWindowExW(0, "STATIC", "GLHidden", Win32.WS_POPUP | Win32.WS_CLIPSIBLINGS | Win32.WS_CLIPCHILDREN,
                0, 0, 64, 64, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
                return;

            hdc = Win32.GetDC(hwnd);
            if (hdc == IntPtr.Zero)
                return;

            var pfd = new Win32.PIXELFORMATDESCRIPTOR
            {
                nSize = (ushort)Marshal.SizeOf<Win32.PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags = Win32.PFD_DRAW_TO_WINDOW | Win32.PFD_SUPPORT_OPENGL | Win32.PFD_DOUBLEBUFFER,
                iPixelType = Win32.PFD_TYPE_RGBA,
                cColorBits = 24,
                cAlphaBits = 8,
                cDepthBits = 16,
                iLayerType = Win32.PFD_MAIN_PLANE
            };

            int pixelFormat = Gdi.ChoosePixelFormat(hdc, ref pfd);
            if (pixelFormat == 0)
                return;

            if (Gdi.SetPixelFormat(hdc, pixelFormat, ref pfd) == 0)
                return;

            hglrc = Wgl.wglCreateContext(hdc);
            if (hglrc == IntPtr.Zero)
                return;

            if (Wgl.wglMakeCurrent(hdc, hglrc) == 0)
                return;

            // Clear simple
            Gl.glViewport(0, 0, 64, 64);
            Gl.glClearColor(0f, 0f, 1f, 1f);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT);
            int err = Gl.glGetError();

            // Cleanup básico
            Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            Wgl.wglDeleteContext(hglrc);
            hglrc = IntPtr.Zero;
            Win32.ReleaseDC(hwnd, hdc);
            hdc = IntPtr.Zero;
            Win32.DestroyWindow(hwnd);
            hwnd = IntPtr.Zero;

            Assert.Equal(Gl.GL_NO_ERROR, err);
        }
        catch (DllNotFoundException)
        {
            // No existe user32/gdi32/opengl32 en runtime: ignorar.
        }
        catch (EntryPointNotFoundException)
        {
            // Alguna función no disponible: ignorar.
        }
        finally
        {
            // Cleanup en caso de early-return
            try { if (hglrc != IntPtr.Zero) { Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero); Wgl.wglDeleteContext(hglrc); } } catch { }
            try { if (hdc != IntPtr.Zero && hwnd != IntPtr.Zero) Win32.ReleaseDC(hwnd, hdc); } catch { }
            try { if (hwnd != IntPtr.Zero) Win32.DestroyWindow(hwnd); } catch { }
        }
    }

    private static class Win32
    {
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WS_CLIPSIBLINGS = 0x04000000;
        public const int WS_CLIPCHILDREN = 0x02000000;

        public const int PFD_DRAW_TO_WINDOW = 0x00000004;
        public const int PFD_SUPPORT_OPENGL = 0x00000020;
        public const int PFD_DOUBLEBUFFER = 0x00000001;
        public const byte PFD_TYPE_RGBA = 0;
        public const byte PFD_MAIN_PLANE = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public byte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateWindowExW(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int X,
            int Y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandleW(string? lpModuleName);
    }

    private static class Gdi
    {
        [DllImport("gdi32.dll")]
        public static extern int ChoosePixelFormat(IntPtr hdc, ref Win32.PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll")]
        public static extern int SetPixelFormat(IntPtr hdc, int iPixelFormat, ref Win32.PIXELFORMATDESCRIPTOR ppfd);
    }

    private static class Wgl
    {
        [DllImport("opengl32.dll")]
        public static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport("opengl32.dll")]
        public static extern int wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        [DllImport("opengl32.dll")]
        public static extern int wglDeleteContext(IntPtr hglrc);
    }

    private static class Gl
    {
        public const int GL_COLOR_BUFFER_BIT = 0x00004000;
        public const int GL_NO_ERROR = 0;

        [DllImport("opengl32.dll")]
        public static extern void glViewport(int x, int y, int width, int height);

        [DllImport("opengl32.dll")]
        public static extern void glClearColor(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll")]
        public static extern void glClear(int mask);

        [DllImport("opengl32.dll")]
        public static extern int glGetError();
    }
}

