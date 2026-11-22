using Veyra.Core.Models;
using Veyra.Core.Services;
using Xunit;

namespace Veyra.Games.Tests;

public sealed class ProfileJsonTests
{
    private static GameProfile Profile() => new()
    {
        Id = "user.abc",
        Name = "Notepad",
        ExecutableName = "notepad.exe",
        Priority = 120,
        AutoSwitch = true,
        AllowDisplayChanges = false,
        Notes = "Test profile"
    };

    [Fact]
    public void ExportImport_RoundTripsFields()
    {
        var json = ProfileJson.ExportGame(Profile());

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.True(result.IsSuccess);
        Assert.NotNull(imported);
        Assert.Equal("Notepad", imported!.Name);
        Assert.Equal("notepad.exe", imported.ExecutableName);
        Assert.Equal(120, imported.Priority);
        Assert.True(imported.AutoSwitch);
        Assert.False(imported.AllowDisplayChanges);
        Assert.Equal("Test profile", imported.Notes);
    }

    [Fact]
    public void Import_AssignsFreshId()
    {
        var json = ProfileJson.ExportGame(Profile());

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.True(result.IsSuccess);
        Assert.NotNull(imported);
        Assert.NotEqual("user.abc", imported!.Id);
        Assert.StartsWith("user.", imported.Id);
    }

    [Fact]
    public void Import_RejectsUnknownMembers()
    {
        var json = """
            {
              "schema": "veyra.game/1",
              "name": "Notepad",
              "executableName": "notepad.exe",
              "runAtStartup": true
            }
            """;

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.False(result.IsSuccess);
        Assert.Null(imported);
    }

    [Fact]
    public void Import_RejectsExecutablePaths()
    {
        var json = """
            {
              "schema": "veyra.game/1",
              "name": "Notepad",
              "executableName": "C:\\Windows\\System32\\notepad.exe"
            }
            """;

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.False(result.IsSuccess);
        Assert.Null(imported);
        Assert.Contains("plain file name", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_RejectsWrongSchema()
    {
        var json = ProfileJson.ExportGame(Profile()).Replace("veyra.game/1", "veyra.game/2", StringComparison.Ordinal);

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.False(result.IsSuccess);
        Assert.Null(imported);
        Assert.Contains("schema", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_RejectsOversizedDocument()
    {
        var json = "{\"schema\":\"veyra.game/1\",\"name\":\"x\",\"notes\":\"" + new string('a', ProfileJson.MaxGameDocumentBytes) + "\"}";

        var result = ProfileJson.ImportGame(json, out var imported);

        Assert.False(result.IsSuccess);
        Assert.Null(imported);
        Assert.Contains("64 KB", result.Message, StringComparison.Ordinal);
    }
}
