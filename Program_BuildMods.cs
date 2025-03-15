using System.IO.Compression;

namespace SC_NewUniversalUpload
{
    internal partial class Program
    {
        /// <summary>
        /// Builds all edited mods via MSBuild.
        /// </summary>
        /// <param name="updatedFiles"></param>
        public void BuildMods(HashSet<string> updatedFiles)
        {
            Console.WriteLine("Changed files:\n-   " + string.Join("\n-   ", updatedFiles));

            int updatedModsCt = 0;

            foreach (var modPath in LocateAllMods(Arguments["--repo"]))
            {
                // If any files in the mod were updated, build it.
                var wasThisModUpdated =
                    updatedFiles.Any(editedFile => Path.Join(Arguments["--repo"], editedFile).Contains(modPath));

                if (!wasThisModUpdated)
                    continue;

                BuildMod(modPath);
                updatedModsCt++;
            }

            Console.WriteLine($"Built {updatedModsCt} mods.");
        }

        private void BuildMod(string modPath)
        {
            Console.WriteLine($"Starting build for {modPath}...");
            // Pull generic SLN files
            ZipFile.ExtractToDirectory(Path.Join(Arguments["--repo"], "VisualStudioSLNs.zip"), modPath);

            RunCmd(Config.MsBuildPath, $"\"{Path.Join(modPath, "Generic.sln")}\" -restore -noWarn:NU1903,CS0649 -verbosity:m", out var stdout); // TODO -verbosity:m
            if (stdout.Contains("error"))
            {
                Console.Error.WriteLine($"Failed to build \"{modPath}\"!");
                Environment.ExitCode = 1;
            }
        }
    }
}
