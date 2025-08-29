using Xunit;

namespace OpenCNCPilot.UI.Tests;

public class UiGlDummyTests
{
    // Esta prueba valida que el filtro de CI excluye Category=UI-GL.
    // No invoca OpenGL ni Skia; sólo asegura que está etiquetada correctamente.
    [Trait("Category", "UI-GL")]
    [Fact]
    public void Dummy_UiGl_Test_Should_Be_Excluded_In_CI()
    {
        // Si esta prueba se ejecuta localmente, debe pasar.
        Assert.True(true);
    }
}
