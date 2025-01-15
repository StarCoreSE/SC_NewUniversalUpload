using System.Text;
using System.Text.RegularExpressions;

namespace SC_NewUniversalUpload
{
    internal partial class Program
    {
        public void LocateAndUploadMods(string[] updatedFiles, string changelog)
        {
            Console.WriteLine("Changelog: " + changelog);
            Console.WriteLine("Changed files:\n-   " + string.Join("\n-   ", updatedFiles));

            int updatedModsCt = 0;

            foreach (var modPath in LocateAllMods(Arguments["--repo"]))
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
                    updatedFiles.Any(editedFile => Path.Join(Arguments["--repo"], editedFile).Contains(modPath)) ||
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

            // Copy all non-ignored files
            var uploadFolder = Path.Join(Config.AppdataModsPath, modId);
            Directory.CreateDirectory(uploadFolder);
            RecursiveCopy(modPath, uploadFolder, ".git", ".github", ".vs", "bin", "obj", "Properties");
            Console.WriteLine($"{modName}: Copied mod to {uploadFolder}.");

            // Generate VDF file (mod metadata for steam)
            var vdfFile = File.CreateText($@"{Config.AppdataModsPath}\item.vdf");
            vdfFile.Write(CreateVdfData(modId, uploadFolder, changelog, modName));
            vdfFile.Flush();
            vdfFile.Close();

            // Invoke SteamCMD and upload the mod
            string stdout;
            RunCmd(Config.SteamCmdPath,
                $@"+login {Config.UploaderAccountName} {Config.UploaderAccountPassword} +workshop_build_item {Config.AppdataModsPath}\item.vdf +quit",
                out stdout);

            Directory.Delete(uploadFolder, true);
            Console.WriteLine($"{modName}: Removed mod from {uploadFolder}.");

            // Generate modinfo.sbmi if needed
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

        private void RecursiveCopy(string source, string destination, params string[] ignoredFolders)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Join(destination, file.Replace(source, "")), true);
            foreach (var folder in Directory.GetDirectories(source).Where(dir => !ignoredFolders.Contains(dir.Replace(source, "").Replace("\\", ""))))
                RecursiveCopy(folder, Path.Join(destination, folder.Replace(source, "")), ignoredFolders);
        }
    }
}
