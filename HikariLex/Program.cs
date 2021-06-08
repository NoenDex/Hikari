/*
Copyright (c) 2020-2021, Stefan Bazelkov
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Fclp;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HikariLex
{
    public class AppArguments
    {
        public string ScriptFile { get; set; }
        public string UserName { get; set; }
        public bool ShowAlert { get; set; }
        public bool HideConsole { get; set; }
        public bool ShowBuildVersion { get; set; }
        public bool GetFirstAvailableLetter { get; set; }
    }

    static class Program
    {
        private static readonly Logger log = LogManager.GetLogger("Hikari");

        private static UserPrincipal user = null;
        private static List<string> groups;
        private static bool doMapping = true;
        private static bool showUserAlert = false;
        private static bool hideConsole = false;
        private static bool showBuildVersion = false;
        private static bool getFirstAvailableLetter = true;
        private static Queue<string> unused;

        private const int ERROR_SW_HIDE = 0;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("winspool.drv", CharSet = CharSet.Unicode)]
        public static extern bool AddPrinterConnection(string pName);

        private static int Main(string[] args)
        {
            LoggingConfiguration config = LogManager.Configuration;
            if (config == null)
            {
                Console.WriteLine("\n~~~~~~~~~~~ Failed to get log configuration ~~~~~~~~~~~\n");
                return -99;
            }
            LoggingRule fileRule = config.FindRuleByName("file rule");
            LoggingRule consoleRule = config.FindRuleByName("console rule");

            fileRule.DisableLoggingForLevels(LogLevel.Info, LogLevel.Fatal);
            LogManager.Configuration = config;

            string script;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            var p = new FluentCommandLineParser<AppArguments>();

            p.Setup(arg => arg.ShowBuildVersion)
                .As('v', "version")
                .Callback(arg =>
                {
                    showBuildVersion = arg;
                })
                .SetDefault(false)
                .WithDescription("Show build version");

            p.Setup(arg => arg.ScriptFile)
                .As('s', "script")
                //.Required()
                .WithDescription("Script file.");

            p.Setup(arg => arg.UserName)
                .As('u', "user")
                .WithDescription("Run script against AD Account name.");

            p.Setup(arg => arg.ShowAlert)
                .As('a', "alert")
                .Callback(arg => showUserAlert = arg)
                .SetDefault(false)
                .WithDescription("Notify the user if error. Default: FALSE (Don't notify)");

            p.Setup(arg => arg.HideConsole)
                .As('h', "hide")
                .Callback(arg => hideConsole = arg)
                .SetDefault(false)
                .WithDescription("Hide console window during execution. Default: FALSE (Don't hide console)");

            p.Setup(arg => arg.GetFirstAvailableLetter)
                .As('f', "first")
                .Callback(arg => getFirstAvailableLetter = arg)
                .SetDefault(false)
                .WithDescription("Resolve drive letter conflict to first available. Default: FALSE (Resolve to next available drive letter)");

            p.SetupHelp("?", "help")
                .Callback(text => Console.WriteLine(text));

            var result = p.Parse(args);

            if (showBuildVersion)
            {
                log.Info(version);
                return 0;
            }

            log.Info("");
            log.Info($"Hikari V{version} - created by Stefan Bazelkov");

            if (result.HelpCalled)
            {
                stopwatch.Stop();
                ShowUsage();
                return 0;
            }

            if (result.EmptyArgs)
            {
                p.HelpOption.ShowHelp(p.Options);
                ShowUsage();
                stopwatch.Stop();
                return 0;
            }

            if (result.HasErrors)
            {
                log.Error(result.ErrorText);
                stopwatch.Stop();
                log.Info($"Time elapsed: {stopwatch.Elapsed.TotalSeconds:00.00}s");
                ShowUserAlert("Command line arguments parsing failed!");
                return -1;
            }

            if (hideConsole)
            {
                // do not show log if console is hidden
                consoleRule.DisableLoggingForLevels(LogLevel.Trace, LogLevel.Fatal);
                ShowWindow(GetConsoleWindow(), ERROR_SW_HIDE);
                log.Info("Start with console window hidden.");
            }

            AppArguments appArg = p.Object;

            if (!File.Exists(appArg.ScriptFile))
            {
                log.Error($"\"{appArg.ScriptFile}\" doesn't exist!");
                stopwatch.Stop();
                log.Info($"Time elapsed: {stopwatch.Elapsed.TotalSeconds:00.00}s");
                ShowUserAlert($"Missing or wrong script file '{appArg.ScriptFile}'!");
                return -2;
            }

            if (!string.IsNullOrEmpty(appArg.UserName))
            {
                user = ActiveDirectoryHelper.FindPrincipal(appArg.UserName);
                if (user == null)
                {
                    stopwatch.Stop();
                    log.Info($"Time elapsed: {stopwatch.Elapsed.TotalSeconds:00.00}s");
                    log.Error($"Error in resolving provided username: \"{appArg.UserName}\"");
                    return -6;
                }
            }

            if (user != null)
            {
                // Do not map network drives if username argument is present
                // Only check the network shares to be connected for that AD user 
                doMapping = false;
                log.Warn($"Checking mappings for \"{user.SamAccountName}\".");
                log.Warn("No network drives will be connected.");
            }
            else
            {
                // logging will be performed only in the console
                fileRule.EnableLoggingForLevels(LogLevel.Info, LogLevel.Fatal);
                LogManager.Configuration = config;
                doMapping = true;
                user = UserPrincipal.Current;
                DisconnectAllLocalNetworkDrives();
                log.Info($"Connecting network drives for \"{user.SamAccountName}\".");
            }

            groups = ActiveDirectoryHelper.GetMembership(user).ToList();

            Console.WriteLine();

            log.Info($"Processing \"{appArg.ScriptFile}\" script ...");

            using (StreamReader file = new StreamReader(appArg.ScriptFile))
            {
                script = file.ReadToEnd();
                file.Close();
            }

            var parser = new HikariScriptParser(groups);
            try
            {
                parser.UserName = user.SamAccountName;
                parser.Parse(script);
                log.Info(parser.Model);
            }
            catch (HikariParserException x)
            {
                Console.WriteLine();
                log.Error(x.Message);
                Console.WriteLine();
                log.Warn("No drives will be connected!");
                Console.WriteLine();
                return -7;
            }

            // Resolve and map the network drives
            ResolveMappings(parser.Model);

            if (doMapping)
            {
                DisconnectAllLocalNetworkDrives();
                ConnectNetworkDrives(parser.Model);
                ConnectNetworkPrinters(parser.Model);
            }

            stopwatch.Stop();
            Console.WriteLine();
            log.Info($"Total time elapsed: {stopwatch.Elapsed.TotalSeconds:00.00}s");
            Console.WriteLine();

            return 0;
        }

        private static string NextDriveLetter(string DriveLetter)
        {
            if (string.IsNullOrWhiteSpace(DriveLetter) || !unused.Any() || DriveLetter.Length < 2)
                return string.Empty;

            char letter = DriveLetter[0];

            // embarrassing conversion to List
            List<string> unusedList = unused.ToList();

            while (true)
            {
                letter = (char)(letter + 1);

                if (letter > 'Z')
                    break;

                string nextDriveLetter = $"{letter}:";

                int index = unusedList.IndexOf(nextDriveLetter);
                if (index > -1)
                {
                    unusedList.RemoveAt(index);
                    // embarrassing conversion back to Queue
                    unused = new Queue<string>(unusedList);
                    return nextDriveLetter;
                }
            }

            return string.Empty;
        }

        public static void ResolveMappings(HikariModel model)
        {
            uint totalConflicts = model.TotalConflicts();
            if (totalConflicts == 0)
            {
                log.Info("No conflicting drive mappings found");
                return;
            }

            Console.WriteLine();
            log.Warn($"There {(totalConflicts == 1 ? "is" : "are")} {totalConflicts} conflict{(totalConflicts == 1 ? string.Empty : "s")} detected.");
            Console.WriteLine();
            // A, B and C are taken for sure! =)
            List<string> alpha = "DEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray().Select(c => c + ":").ToList();

            List<string> used = model.UsedDriveLetters();

            // Only in real-run exclude local drives other than A-C
            if (doMapping)
            {
                // All local fixed drives & CD/DVD drives
                List<string> localDrives = DriveInfo.GetDrives()
                    .ToList()
                    .Where(d => d.DriveType == DriveType.Fixed || d.DriveType == DriveType.CDRom)
                    .Select(d => d.Name.Substring(0, 2).ToUpper())
                    .ToList();
                // Add local fixed drives to used drives
                used.AddRange(localDrives);

                log.Info($"Excluding locally found drives: [{string.Join(", ", localDrives.ToArray())}]");
                Console.WriteLine();
            }

            // Generate unused drive letters queue out of all possible excluding used and local fixed drives
            unused = new Queue<string>(alpha.Where(l => used.All(c => c != l)));

            // Try moving until resolved or no more available drives
            while (model.TotalConflicts() > 0 && unused.Count > 0)
            {
                string drive = model.GetNextConflictDriveIndex();
                if (!string.IsNullOrWhiteSpace(drive))
                {
                    if (!getFirstAvailableLetter)
                    {
                        // compute next drive letter
                        string nextDrive = NextDriveLetter(drive);
                        log.Info($"Next drive: \"{nextDrive}\"");
                        model.MoveFirstUNCConflict(drive, nextDrive);
                    }
                    else
                    {
                        // Pop 1st available
                        string unusedDrive = unused.Dequeue();
                        model.MoveFirstUNCConflict(drive, unusedDrive);
                    }
                }
                if (unused.Count == 0)
                {
                    Console.WriteLine();
                    log.Warn("No more drive letters available!");
                    log.Warn("All unresolved drives will be reduced to the 1st UNC!");
                    model.EmergencyReduceUNCs();
                }
            }

            Console.WriteLine();

            log.Info("Final mappings result:");
            log.Info(model);
        }

        private static void ShowUserAlert(string message = null)
        {
            if (showUserAlert)
            {
                string msg = "There is a problem connecting some of your network drives.\nPlease, contact Service Desk to resolve the issue.";
                if (!string.IsNullOrEmpty(message))
                    msg = $"{msg}\n\nDetails: {message}";
                MessageBox.Show(msg, "Connect network drives", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage: Hikari.exe /s or /script file.hikari [/u or /user ADUserAccount]");
            Console.WriteLine("Example: Hikari.exe /script ScriptFile.hikari /u DenkOr");
            Console.WriteLine("\tThis example usage will run drive mapping resolution of AD account DenkOr against ScriptFile.hikari");
            Console.WriteLine("\tand will display resolved drives and network location for each of them.");
            Console.WriteLine();
            Console.WriteLine("Example: Hikari.exe /script ScriptFile.hikari");
            Console.WriteLine("\tThis example usage will resolve and connect drive mappings for the current user.");
            Console.WriteLine();
            Console.WriteLine("\tIf user argument is present no network mapping will occur.");
            Console.WriteLine("\tIf user argument is not present network mapping will be processed.");
            Console.WriteLine();
        }

        private static bool ConnectNetworkDrives(HikariModel model)
        {
            log.Info("Connecting network drives...");
            Console.WriteLine();
            bool success = true;
            Forker driveForker = new Forker();

            foreach (var drive in model.Drives)
            {
                Tuple<string, string> exp_unc = (drive.Value).FirstOrDefault();
                string driveLetter = drive.Key;
                string expression = exp_unc.Item1;
                string unc = exp_unc.Item2;
                driveForker.Fork(delegate
                {
                    if (!Directory.Exists(unc))
                    {
                        log.Error($"Network path \"{unc}\" doesn't exist! Drive {driveLetter} for [{expression}] is NOT connected!");
                        success = false;
                    }
                    else
                    {
                        string result = InvokeWindowsNetworking.connectToRemote(driveLetter, unc);
                        if (!string.IsNullOrEmpty(result))
                        {
                            log.Error($"Network drive {driveLetter} -> \"{unc}\" ERROR: {result}");
                            success = false;
                        }
                        else
                        {
                            log.Info($"{driveLetter} -> \"{unc}\" [{expression}]");
                        }
                    }
                });
            }

            driveForker.Join();

            return success;
        }

        private static bool ConnectNetworkPrinters(HikariModel model)
        {
            if (model.Printers.Count == 0)
                return true;

            Console.WriteLine();
            log.Info("Connecting network printers...");
            Console.WriteLine();

            bool success = true;
            Forker printerForker = new Forker();
            foreach (Tuple<string, string> printer in model.Printers)
            {
                printerForker.Fork(delegate
                {
                    if (!AddPrinterConnection(printer.Item2))
                    {
                        log.Error($"Error connecting network printer \"{printer.Item2}\"");
                        success = false;
                    }
                    else
                    {
                        log.Info($"[{printer.Item1}] -> \"{printer.Item2}\"");
                    }
                });
            }

            printerForker.Join();
            return success;
        }

        private static void DisconnectAllLocalNetworkDrives()
        {
            Console.WriteLine();
            foreach (DriveInfo networkDrive in DriveInfo.GetDrives().Where(
                (drive) => drive.DriveType == DriveType.Network))
            {
                //Shave the "\" and call drop function.  Note:  Remaps on relogin and doesn't force close.
                string networkDriveName = networkDrive.Name.Substring(0, 2);
                //Force disconnect network shares
                int result = InvokeWindowsNetworking.DropNetworkConnection(networkDriveName, 0, true);

                switch (result)
                {
                    case InvokeWindowsNetworking.NO_ERROR: //Success
                        log.Info($"Disconnect drive: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_BAD_PROFILE:
                        log.Error($"Disconnect drive - Bad profile: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_CANNOT_OPEN_PROFILE:
                        log.Error($"Disconnect drive - Cannot open profile: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_DEVICE_IN_USE:
                        log.Error($"Disconnect drive - Device in use: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_EXTENDED_ERROR:
                        log.Error($"Disconnect drive - Extended error: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_NOT_CONNECTED:
                        log.Error($"Disconnect drive - Device not connected: {networkDriveName}");
                        break;
                    case InvokeWindowsNetworking.ERROR_OPEN_FILES:
                        log.Error($"Disconnect drive - Error open files: {networkDriveName}");
                        break;
                    default:
                        log.Error($"Disconnect drive - Unknown error code: {networkDriveName}");
                        break;
                }
            }
            Console.WriteLine();
        }
    }
}
