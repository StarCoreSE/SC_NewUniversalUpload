using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SC_NewUniversalUpload;

internal class ConfigWrapper
{
    [RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
    public static ConfigWrapper Read(string configPath)
    {
        configPath = configPath[..configPath.LastIndexOf('\\')] + @"\config.json";

        return JsonSerializer.Deserialize<ConfigWrapper>(File.ReadAllText(configPath)) ?? throw new NullReferenceException($"{configPath} could not be read!");
    }

    public required string AppdataModsPath { get; init; }
    public string SteamCmdPath { get; init; } = @"C:\steamcmd\steamcmd.exe";
    public string MsBuildPath { get; init; } = @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"; // TODO: Change

    public required string UploaderAccountName { get; init; }
    public required string UploaderAccountPassword { get; init; }
    public required string UploaderAccountSteamId { get; init; }

    public string DefaultGitBranchName { get; init; } = "main";

    public string SteamAppId { get; init; } = "24485";
    public string DefaultWorkshopVisibility { get; init; } = "3";
    public string DefaultDescription { get; init; } = "Uploaded from GitHub.";
}