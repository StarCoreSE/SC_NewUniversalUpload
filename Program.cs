using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using SC_NewUniversalUpload.Utilities;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SC_NewUniversalUpload;

internal class Program
{
    public static ConfigWrapper Config;
    public static ArgumentParser Arguments;

    private static void Main(string[] args)
    {
        Config = ConfigWrapper.Read(Path.Join(typeof(Program).Assembly.Location.Remove(typeof(Program).Assembly.Location.LastIndexOf('\\')), "config.json"));

        if (args.Length == 0)
        {
            Console.WriteLine("No arguments were provided!");
            return;
        }

        Arguments = new ArgumentParser(args);

        var thisBranch = File.ReadAllText(
            Path.Join(Arguments["--repo"], @".git\HEAD")
            ).Trim();
        thisBranch = thisBranch[(thisBranch.LastIndexOf('/') + 1)..];

        switch (args[0])
        {
            case "deletebranch":
                new Program(args[1]).DeleteBranch();
                break;
            case "uploadall":
                new Program(thisBranch).ForceUploadAll();
                break;
            default:
                new Program(thisBranch).LocateAndUploadMods(Arguments["--changes"].Split(','), Arguments["--changelog"] ?? "No changelog specified.");
                break;
        }
    }


    public string Branch;

    public Program(string branch)
    {
        Console.WriteLine("Current branch: " + branch);
        Branch = branch;
    }

    #region ForceUploadAll

    public void ForceUploadAll()
    {
        foreach (var mod in LocateAllMods(Config.RepositoryPath))
        {
            UploadMod(mod, "Force-uploaded.");
        }
    }

    #endregion

    #region Delete Branch

    // This doesn't work :(
    public void DeleteBranch()
    {
        var branchModIds = LocateAllModInfos(Config.RepositoryPath).Where(path => path.Contains(Branch)).Select(RetrieveModId);
        Console.WriteLine("Workshop mods to be deleted:");
        foreach (var modId in branchModIds)
        {
            Console.Write("- " + modId);

            Console.WriteLine(" (finished)");
        }
    }

    #endregion

    #region Upload Mods

    public void LocateAndUploadMods(string[] updatedFiles, string changelog)
    {
        Console.WriteLine("Changelog: " + changelog);
        Console.WriteLine("Changed files:\n-   " + string.Join("\n-   ", updatedFiles));

        int updatedModsCt = 0;

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
                !File.Exists($@"{modPath}\modinfo_{Branch}.sbmi");

            if (!wasThisModUpdated)
                continue;

            UploadMod(modPath, changelog);
            updatedModsCt++;
        }

        Console.WriteLine($"Updated {updatedModsCt} mods.");
    }

    /// <summary>
    ///     Uploads a mod via SteamCMD.
    /// </summary>
    /// <param name="modPath"></param>
    /// <param name="Branch"></param>
    /// <param name="changelog"></param>
    private void UploadMod(string modPath, string changelog)
    {
        var modName = modPath.Substring(modPath.LastIndexOf('\\') + 1);
        var modId = "";
        Console.WriteLine($"{modName}: Mod was updated.");

        if (File.Exists($@"{modPath}\modinfo_{Branch}.sbmi"))
            modId = RetrieveModId($@"{modPath}\modinfo_{Branch}.sbmi");
        Console.WriteLine($"{modName}: ModId: " + modId);

        var vdfFile = File.CreateText($@"{Config.AppdataModsPath}\item.vdf");

        vdfFile.Write(CreateVdfData(modId, modPath, changelog, modName));
        vdfFile.Flush();
        vdfFile.Close();

        string stdout;
        RunSteamCmd(
            $@"+login {Config.UploaderAccountName} {Config.UploaderAccountPassword} +workshop_build_item {Config.AppdataModsPath}\item.vdf +quit",
            out stdout);
        if (modId == "")
        {
            modId = Regex.Match(stdout, @"(?<=PublishFileID )\d*").Value;
            Console.WriteLine("New ModID: " + modId);

            var sbmiFile = File.CreateText($@"{modPath}\modinfo_{Branch}.sbmi");
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

    private string CreateVdfData(string modId, string modPath, string changelog, string modName = "")
    {
        StringBuilder vdfFile = new StringBuilder();

        vdfFile.AppendLine("\"workshopitem\"");
        vdfFile.AppendLine("{");
        vdfFile.AppendLine($"    \"appid\" \"{Config.SteamAppId}\"");

        if (modId == "")
        {
            if (File.Exists($"{modPath}\\thumb.jpg"))
                vdfFile.AppendLine($"    \"previewfile\" \"{modPath}\\thumb.jpg\"");
            vdfFile.AppendLine($"    \"visibility\" \"{Config.DefaultWorkshopVisibility}\"");
            vdfFile.AppendLine($"    \"title\" \"{modName}_{Branch}\"");
            vdfFile.AppendLine(
                $"    \"description\" \"{Config.DefaultDescription}\"");
        }
        else
        {
            vdfFile.AppendLine($"    \"publishedfileid\" \"{modId}\"");
        }

        vdfFile.AppendLine($"    \"contentfolder\" \"{modPath}\"");
        vdfFile.AppendLine($"    \"changenote\" \"{changelog}\"");
        vdfFile.AppendLine("}");

        return vdfFile.ToString();
    }

    #endregion

    #region Static Methods

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
                #if (DEBUG)
                Console.WriteLine(line);
                #endif
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

    private static string RetrieveModId(string filePath)
    {
        return Regex.Match(File.ReadAllText(filePath), @"\d*(?=<\/Id>)").Value;
    }

    #endregion
}