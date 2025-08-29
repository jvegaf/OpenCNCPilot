using System;
using System.Runtime.InteropServices;
using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class UiGlSmokeTests
{
    [Trait("Category", "UI-GL")]
    [Fact]
    public void Offscreen_OpenGL_Context_Can_Clear_Color_When_Available()
    {
        // Objetivo: crear un contexto OpenGL offscreen con EGL (Linux) y ejecutar glClear.
        // Si EGL/GL no está disponible en el entorno, salir silenciosamente (prueba pasa localmente cuando no hay soporte).

        if (!OperatingSystem.IsLinux())
            return; // Implementación minimal para Linux; en otros OS, no hace nada.

        try
        {
            if (!Egl.EglAvailable || !Gl.GlAvailable)
                return; // bibliotecas no disponibles

            IntPtr display = Egl.eglGetDisplay(Egl.EGL_DEFAULT_DISPLAY);
            if (display == IntPtr.Zero)
                return; // no display

            if (Egl.eglInitialize(display, out _, out _) == 0)
                return; // no init

            int[] attribs =
            {
                Egl.EGL_SURFACE_TYPE, Egl.EGL_PBUFFER_BIT,
                Egl.EGL_RENDERABLE_TYPE, Egl.EGL_OPENGL_BIT,
                Egl.EGL_RED_SIZE, 8,
                Egl.EGL_GREEN_SIZE, 8,
                Egl.EGL_BLUE_SIZE, 8,
                Egl.EGL_ALPHA_SIZE, 8,
                Egl.EGL_NONE
            };
            IntPtr[] configs = new IntPtr[1];
            int numConfigs;
            if (Egl.eglChooseConfig(display, attribs, configs, configs.Length, out numConfigs) == 0 || numConfigs == 0)
            {
                Egl.eglTerminate(display);
                return; // no config
            }

            int[] pbufAttribs = { Egl.EGL_WIDTH, 64, Egl.EGL_HEIGHT, 64, Egl.EGL_NONE };
            IntPtr surface = Egl.eglCreatePbufferSurface(display, configs[0], pbufAttribs);
            if (surface == IntPtr.Zero)
            {
                Egl.eglTerminate(display);
                return; // no surface
            }

            if (Egl.eglBindAPI(Egl.EGL_OPENGL_API) == 0)
            {
                Egl.eglDestroySurface(display, surface);
                Egl.eglTerminate(display);
                return; // no GL API
            }

            IntPtr context = Egl.eglCreateContext(display, configs[0], IntPtr.Zero, IntPtr.Zero);
            if (context == IntPtr.Zero)
            {
                Egl.eglDestroySurface(display, surface);
                Egl.eglTerminate(display);
                return; // no context
            }

            if (Egl.eglMakeCurrent(display, surface, surface, context) == 0)
            {
                Egl.eglDestroyContext(display, context);
                Egl.eglDestroySurface(display, surface);
                Egl.eglTerminate(display);
                return; // no current
            }

            // Hacer un clear y validar error == 0
            Gl.glViewport(0, 0, 64, 64);
            Gl.glClearColor(0f, 1f, 0f, 1f);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT);
            int err = Gl.glGetError();

            // Limpieza
            Egl.eglMakeCurrent(display, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Egl.eglDestroyContext(display, context);
            Egl.eglDestroySurface(display, surface);
            Egl.eglTerminate(display);

            Assert.Equal(Gl.GL_NO_ERROR, err);
        }
        catch (DllNotFoundException)
        {
            // No hay EGL/GL en el sistema: no ejecutamos la ruta GL.
        }
        catch (EntryPointNotFoundException)
        {
            // Alguna función EGL/GL no existente: ignorar.
        }
    }

    private static class Egl
    {
        public const int EGL_NONE = 0x3038;
        public const int EGL_WIDTH = 0x3057;
        public const int EGL_HEIGHT = 0x3056;
        public const int EGL_SURFACE_TYPE = 0x3033;
        public const int EGL_PBUFFER_BIT = 0x0001;
        public const int EGL_RENDERABLE_TYPE = 0x3040;
        public const int EGL_OPENGL_BIT = 0x0008;
        public const int EGL_RED_SIZE = 0x3024;
        public const int EGL_GREEN_SIZE = 0x3023;
        public const int EGL_BLUE_SIZE = 0x3022;
        public const int EGL_ALPHA_SIZE = 0x3021;
        public const int EGL_OPENGL_API = 0x30A2;
        public static readonly IntPtr EGL_DEFAULT_DISPLAY = IntPtr.Zero;

        public static bool EglAvailable => true;

        [DllImport("libEGL.so.1")]
        public static extern IntPtr eglGetDisplay(IntPtr display_id);

        [DllImport("libEGL.so.1")]
        public static extern int eglInitialize(IntPtr dpy, out int major, out int minor);

        [DllImport("libEGL.so.1")]
        public static extern int eglChooseConfig(IntPtr dpy, int[] attrib_list, IntPtr[] configs, int config_size, out int num_config);

        [DllImport("libEGL.so.1")]
        public static extern IntPtr eglCreatePbufferSurface(IntPtr dpy, IntPtr config, int[] attrib_list);

        [DllImport("libEGL.so.1")]
        public static extern int eglBindAPI(int api);

        [DllImport("libEGL.so.1")]
        public static extern IntPtr eglCreateContext(IntPtr dpy, IntPtr config, IntPtr share_context, IntPtr attrib_list);

        [DllImport("libEGL.so.1")]
        public static extern int eglMakeCurrent(IntPtr dpy, IntPtr draw, IntPtr read, IntPtr ctx);

        [DllImport("libEGL.so.1")]
        public static extern int eglDestroyContext(IntPtr dpy, IntPtr ctx);

        [DllImport("libEGL.so.1")]
        public static extern int eglDestroySurface(IntPtr dpy, IntPtr surface);

        [DllImport("libEGL.so.1")]
        public static extern int eglTerminate(IntPtr dpy);
    }

    private static class Gl
    {
        public const int GL_COLOR_BUFFER_BIT = 0x00004000;
        public const int GL_NO_ERROR = 0;

        public static bool GlAvailable => true;

        [DllImport("libGL.so.1")]
        public static extern void glViewport(int x, int y, int width, int height);

        [DllImport("libGL.so.1")]
        public static extern void glClearColor(float red, float green, float blue, float alpha);

        [DllImport("libGL.so.1")]
        public static extern void glClear(int mask);

        [DllImport("libGL.so.1")]
        public static extern int glGetError();
    }
}
