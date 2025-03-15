using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using SC_NewUniversalUpload.Utilities;

namespace SC_NewUniversalUpload;

internal partial class Program
{
    public static readonly ConfigWrapper Config = ConfigWrapper.Read(Path.Join(typeof(Program).Assembly.Location.Remove(typeof(Program).Assembly.Location.LastIndexOf('\\')), "config.json"));
    public static ArgumentParser Arguments;

    private static void Main(string[] args)
    {
        #if DEBUG
        const string releaseType = "DEBUG";
        #else
        const string releaseType = "RELEASE";
        #endif
        
        Console.WriteLine($"NewUniversalUpload - [{releaseType}]\nby Aristeas\n======================");

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
                Console.WriteLine("DeleteBranch invoked.");
                new Program(args[1]).DeleteBranch();
                break;
            case "uploadall":
                Console.WriteLine("UploadAll invoked.");
                new Program(thisBranch).ForceUploadAll();
                break;
            case "build":
                Console.WriteLine("Build invoked.");
                new Program(thisBranch).BuildMods(GetChangedFiles(Arguments["--repo"]));
                break;
            default:
                Console.WriteLine("Upload invoked.");
                new Program(thisBranch).LocateAndUploadMods(GetChangedFiles(Arguments["--repo"]), Arguments["--changelog"] ?? "No changelog specified.");
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
        foreach (var mod in LocateAllMods(Arguments["--repo"]))
        {
            UploadMod(mod, "Force-uploaded.");
        }
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
    ///     Triggers a process, and locks the current thread until it is completed.
    /// </summary>
    /// <param name="args">Space-seperated arguments.</param>
    /// <param name="stdout">The console output from the process.</param>
    private static void RunCmd(string executablePath, string args, out string stdout, string workingDirectory = "")
    {
        #if (DEBUG)
        Console.WriteLine($"Now executing \"{executablePath}\"");
        #endif
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            }
        };
        process.Start();

        var stdoutBuilder = new StringBuilder();
        while (!process.HasExited)
        {
            if (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadToEnd();
                #if (DEBUG)
                Console.Write(line);
                #endif
                stdoutBuilder.Append(line);
            }

            if (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadToEnd();
                Console.Error.Write(line);
            }
        }

        if (!process.StandardOutput.EndOfStream)
        {
            var line = process.StandardOutput.ReadToEnd();
            #if (DEBUG)
            Console.Write(line);
            #endif
            stdoutBuilder.Append(line);
        }

        if (!process.StandardError.EndOfStream)
        {
            var line = process.StandardError.ReadToEnd();
            Console.Error.Write(line);
        }

        #if (DEBUG)
        Console.WriteLine("\nFinished execution.");
        #endif

        stdout = stdoutBuilder.ToString();
    }

    private static string RetrieveModId(string filePath)
    {
        return Regex.Match(File.ReadAllText(filePath), @"\d*(?=<\/Id>)").Value;
    }

    private static HashSet<string> GetChangedFiles(string repoPath)
    {
        // git show --name-only --oneline
        // Show changelog with format:
        // <hash> (<TO> -> <FROM>) <commit>
        // <editfile>
        // <nexteditfile>
        RunCmd("git", "show --name-only --pretty=oneline", out string outstr, repoPath);

        HashSet<string> validPaths = new();
        foreach (var str in outstr.Split("\n"))
        {
            if (string.IsNullOrWhiteSpace(str) || str.IndexOf(' ') == 40)
                continue;
            string path = Path.Join(repoPath, str.Trim().Replace('/', '\\'));
            validPaths.Add(path);
        }

        return validPaths;
    }

    #endregion
}