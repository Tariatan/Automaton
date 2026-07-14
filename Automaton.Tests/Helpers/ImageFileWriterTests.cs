using Automaton.Helpers;
using OpenCvSharp;

namespace Automaton.Tests.Helpers;

public sealed class ImageFileWriterTests
{
    [Fact]
    public void WriteImage_OutputDirectoryMissing_CreatesDirectoryAndWritesImage()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var outputPath = Path.Combine(workspace.Path, "nested", "capture.png");
        using var image = new Mat(new Size(4, 3), MatType.CV_8UC3, Scalar.White);

        // Act
        ImageFileWriter.WriteImage(outputPath, image);

        // Assert
        Assert.True(File.Exists(outputPath));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(outputPath)!, "*.tmp"));
        using var writtenImage = Cv2.ImRead(outputPath);
        Assert.False(writtenImage.Empty());
        Assert.Equal(new Size(4, 3), writtenImage.Size());
    }

    [Fact]
    public void WriteImage_OutputPathExists_ReplacesImage()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var outputPath = Path.Combine(workspace.Path, "capture.png");
        using var firstImage = new Mat(new Size(3, 2), MatType.CV_8UC3, Scalar.Black);
        using var secondImage = new Mat(new Size(3, 2), MatType.CV_8UC3, Scalar.White);
        ImageFileWriter.WriteImage(outputPath, firstImage);

        // Act
        ImageFileWriter.WriteImage(outputPath, secondImage);

        // Assert
        using var writtenImage = Cv2.ImRead(outputPath);
        Assert.False(writtenImage.Empty());
        var sum = Cv2.Sum(writtenImage);
        Assert.Equal(secondImage.Width * secondImage.Height * 255, sum.Val0);
        Assert.Equal(secondImage.Width * secondImage.Height * 255, sum.Val1);
        Assert.Equal(secondImage.Width * secondImage.Height * 255, sum.Val2);
    }
}
