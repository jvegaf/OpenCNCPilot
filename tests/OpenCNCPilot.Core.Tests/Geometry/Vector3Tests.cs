using FluentAssertions;
using OpenCNCPilot.Core.Geometry;

namespace OpenCNCPilot.Core.Tests.Geometry;

public class Vector3Tests
{
    [Fact]
    public void Constructor_WithDoubles_ShouldSetValues()
    {
        // Arrange & Act
        var vector = new Vector3(1.5, 2.5, 3.5);

        // Assert
        vector.X.Should().Be(1.5);
        vector.Y.Should().Be(2.5);
        vector.Z.Should().Be(3.5);
    }

    [Fact]
    public void Addition_ShouldWorkCorrectly()
    {
        // Arrange
        var v1 = new Vector3(1, 2, 3);
        var v2 = new Vector3(4, 5, 6);

        // Act
        var result = v1 + v2;

        // Assert
        result.X.Should().Be(5);
        result.Y.Should().Be(7);
        result.Z.Should().Be(9);
    }

    [Fact]
    public void Magnitude_ShouldCalculateCorrectly()
    {
        // Arrange
        var vector = new Vector3(3, 4, 0);

        // Act
        var magnitude = vector.Magnitude;

        // Assert
        magnitude.Should().Be(5.0);
    }

    [Fact]
    public void CrossProduct_ShouldWorkCorrectly()
    {
        // Arrange
        var v1 = new Vector3(1, 0, 0);
        var v2 = new Vector3(0, 1, 0);

        // Act
        var result = Vector3.CrossProduct(v1, v2);

        // Assert
        result.X.Should().Be(0);
        result.Y.Should().Be(0);
        result.Z.Should().Be(1);
    }

    [Fact]
    public void DotProduct_ShouldWorkCorrectly()
    {
        // Arrange
        var v1 = new Vector3(1, 2, 3);
        var v2 = new Vector3(4, 5, 6);

        // Act
        var result = Vector3.DotProduct(v1, v2);

        // Assert
        result.Should().Be(32); // 1*4 + 2*5 + 3*6 = 4 + 10 + 18 = 32
    }

    [Fact]
    public void Parse_ShouldWorkWithValidInput()
    {
        // Arrange
        var input = "1.5,2.5,3.5";

        // Act
        var result = Vector3.Parse(input);

        // Assert
        result.X.Should().Be(1.5);
        result.Y.Should().Be(2.5);
        result.Z.Should().Be(3.5);
    }

    [Fact]
    public void Parse_ShouldThrowForInvalidInput()
    {
        // Arrange
        var input = "1.5,2.5"; // Only 2 components

        // Act & Assert
        Assert.Throws<FormatException>(() => Vector3.Parse(input));
    }
}
