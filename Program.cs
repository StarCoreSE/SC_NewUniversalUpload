using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SC_NewUniversalUpload;

internal static class Program
{
    public static ConfigWrapper Config;

    private static void Main(string[] args)
    {
        string configPath = typeof(Program).Assembly.Location;
        configPath = configPath.Substring(0, configPath.LastIndexOf('\\')) + @"\config.json";

        Config = JsonSerializer.Deserialize<ConfigWrapper>(File.ReadAllText(configPath)) ?? throw new NullReferenceException($"{configPath} could not be read!");

        var updatedFiles = args[0].Split(',');
        var changelog = args.Length > 1 ? args[1] : "No changelog specified.";
        var branch = File.ReadAllText(Config.RepositoryPath + @".git\HEAD").Trim();
        branch = branch.Substring(branch.LastIndexOf('/') + 1);
        Console.WriteLine("Current branch: " + branch);
        Console.WriteLine("Changelog: " + changelog);
        Console.WriteLine("Changed files:\n-   " + string.Join("\n-   ", updatedFiles));

        var updatedModsCt = 0;

        foreach (var modPath in LocateAllMods(Config.RepositoryPath))
        {
            // Remove old ModInfo.sbmi files. Technically they do no harm, but it's good for consistency.
            if (File.Exists(modPath + @"\modinfo.sbmi"))
            {
                try
                {
                    File.Copy(modPath + @"\modinfo.sbmi", modPath + $@"\modinfo_{Config.DefaultGitBranchName}.sbmi");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                File.Delete(modPath + @"\modinfo.sbmi");
            }

            // If any files in the mod were updated OR we're creating a new branch, upload it.
            var wasThisModUpdated =
                updatedFiles.Any(editedFile => (Config.RepositoryPath + editedFile).Contains(modPath)) ||
                !File.Exists($@"{modPath}\modinfo_{branch}.sbmi");

            if (!wasThisModUpdated)
                continue;

            UploadMod(modPath, branch, changelog);
            updatedModsCt++;
        }

        Console.WriteLine($"Updated {updatedModsCt} mods.");
    }

    /// <summary>
    ///     Recursively locates all folders containing a *.sbmi file.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static string[] LocateAllMods(string path)
    {
        var modInfos = LocateAllModInfos(path);
        var modFolders = new HashSet<string>();
        foreach (var modInfo in modInfos)
            modFolders.Add(modInfo.Substring(0, modInfo.LastIndexOf(@"\", StringComparison.Ordinal)));

        return modFolders.ToArray();
    }

    /// <summary>
    ///     Recursively locates all *.sbmi files within a directory.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static string[] LocateAllModInfos(string path)
    {
        var allSbmis = Directory.GetFiles(path, "*.sbmi").ToList();

        foreach (var directory in Directory.GetDirectories(path))
            allSbmis.AddRange(LocateAllModInfos(directory));

        return allSbmis.ToArray();
    }

    /// <summary>
    ///     Uploads a mod via SteamCMD.
    /// </summary>
    /// <param name="modPath"></param>
    /// <param name="branch"></param>
    /// <param name="changelog"></param>
    private static void UploadMod(string modPath, string branch, string changelog)
    {
        var modName = modPath.Substring(modPath.LastIndexOf('\\') + 1);
        var modId = "";
        Console.WriteLine($"{modName}: Mod was updated.");

        if (File.Exists($@"{modPath}\modinfo_{branch}.sbmi"))
            modId = Regex.Match(File.ReadAllText($@"{modPath}\modinfo_{branch}.sbmi"), @"\d*(?=<\/Id>)").Value;
        Console.WriteLine($"{modName}: ModId: " + modId);

        var vdfFile = File.CreateText($@"{Config.AppdataModsPath}\item.vdf");

        if (modId == "")
        {
            vdfFile.WriteLine($"\"workshopitem\"");
            vdfFile.WriteLine("{");
            vdfFile.WriteLine($"    \"appid\" \"{Config.SteamAppId}\"");
            vdfFile.WriteLine($"    \"previewfile\" \"{modPath}\\thumb.jpg\"");
            vdfFile.WriteLine($"    \"visibility\" \"{Config.DefaultWorkshopVisibility}\"");
            vdfFile.WriteLine($"    \"title\" \"{modName}_{branch}\"");
            vdfFile.WriteLine(
                $"    \"description\" \"{Config.DefaultDescription}\"");
            vdfFile.WriteLine($"    \"contentfolder\" \"{modPath}\"");
            vdfFile.WriteLine($"    \"changenote\" \"{changelog}\"");
            vdfFile.WriteLine("}");
        }
        else
        {
            vdfFile.WriteLine("\"workshopitem\"");
            vdfFile.WriteLine("{");
            vdfFile.WriteLine($"    \"appid\" \"{Config.SteamAppId}\"");
            vdfFile.WriteLine($"    \"publishedfileid\" \"{modId}\"");
            vdfFile.WriteLine($"    \"contentfolder\" \"{modPath}\"");
            vdfFile.WriteLine($"    \"changenote\" \"{changelog}\"");
            vdfFile.WriteLine("}");
        }

        vdfFile.Flush();
        vdfFile.Close();

        string stdout;
        RunSteamCmd(
            $"+login {Config.UploaderAccountName} {Config.UploaderAccountPassword} +workshop_build_item {Config.AppdataModsPath}\\item.vdf +quit",
            out stdout);
        if (modId == "")
        {
            modId = Regex.Match(stdout, @"(?<=PublishFileID )\d*").Value;
            Console.WriteLine("New ModID: " + modId);

            var sbmiFile = File.CreateText($@"{modPath}\modinfo_{branch}.sbmi");
            {
                sbmiFile.WriteLine("<?xml version=\"1.0\"?>");
                sbmiFile.WriteLine(
                    "<MyObjectBuilder_ModInfo xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
                sbmiFile.WriteLine($"  <SteamIDOwner>{Config.UploaderAccountSteamId}</SteamIDOwner>");
                sbmiFile.WriteLine("  <WorkshopId>0</WorkshopId>");
                sbmiFile.WriteLine("  <WorkshopIds>");
                sbmiFile.WriteLine("    <WorkshopId>");
                sbmiFile.WriteLine($"      <Id>{modId}</Id>");
                sbmiFile.WriteLine("      <ServiceName>Steam</ServiceName>");
                sbmiFile.WriteLine("    </WorkshopId>");
                sbmiFile.WriteLine("  </WorkshopIds>");
                sbmiFile.WriteLine("</MyObjectBuilder_ModInfo>");
            }
            sbmiFile.Flush();
            sbmiFile.Close();
        }

        Console.WriteLine($"{modName}: Finished uploading!");
    }

    /// <summary>
    ///     Triggers the SteamCMD process, and locks the current thread until it is completed.
    /// </summary>
    /// <param name="args">Space-seperated arguments.</param>
    /// <param name="stdout">The console output from SteamCMD.</param>
    private static void RunSteamCmd(string args, out string stdout)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                //FileName = Config.Bin64Path + "SEWorkshopTool.exe",
                FileName = Config.SteamCmdPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                RedirectStandardError = true
            }
        };
        process.Start();

        var stdoutBuilder = new StringBuilder();
        while (!process.HasExited)
        {
            if (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                //Console.WriteLine(line);
                stdoutBuilder.AppendLine(line);
            }

            if (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLine();
                Console.Error.WriteLine(line);
            }
        }

        stdout = stdoutBuilder.ToString();
    }
}