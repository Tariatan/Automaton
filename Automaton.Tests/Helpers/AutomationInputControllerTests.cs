using System.Reflection;
using System.Runtime.InteropServices;
using Automaton.Helpers;

namespace Automaton.Tests.Helpers;

public sealed class AutomationInputControllerTests
{
    [Fact]
    public void SendKeyboardInput_Win32InputStructureMarshaled_MatchesExpectedSize()
    {
        // Arrange
        var inputType = typeof(AutomationInputController).GetNestedType("INPUT", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AutomationInputController.INPUT was not found.");
        var expectedSize = IntPtr.Size == 8 ? 40 : 28;

        // Act
        var actualSize = Marshal.SizeOf(inputType);

        // Assert
        Assert.Equal(expectedSize, actualSize);
    }
}
