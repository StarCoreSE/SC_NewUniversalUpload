namespace SC_NewUniversalUpload;

internal class ConfigWrapper
{
    public string RepositoryPath { get; set; }
    public string AppdataModsPath { get; set; }
    public string SteamCmdPath { get; set; } = @"C:\steamcmd\steamcmd.exe";

    public string UploaderAccountName { get; set; }
    public string UploaderAccountPassword { get; set; }
    public string UploaderAccountSteamId { get; set; }

    public string DefaultGitBranchName { get; set; } = "main";

    public string SteamAppId { get; set; } = "24485";
    public string DefaultWorkshopVisibility { get; set; } = "3";
    public string DefaultDescription { get; set; }
}