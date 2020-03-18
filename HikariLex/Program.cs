/*
Copyright (c) 2020, Stefan Bazelkov
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HikariLex
{
    public class AppArguments
    {
        public string ScriptFile { get; set; }
        public string UserName { get; set; }
        public bool ShowAlert { get; set; }
        public bool HideConsole { get; set; }
    }

    class Program
    {
        private static readonly Logger log = LogManager.GetLogger("Hikari");

        private static UserPrincipal user = null;
        private static List<string> groups;
        private static bool doMapping = true;
        private static bool showUserAlert = false;
        private static bool hideConsole = false;

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        static int Main(string[] args)
        {
            string script = string.Empty;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Info("");
            log.Info("Hikari V{0} - created by Stefan Bazelkov", version);

            var p = new FluentCommandLineParser<AppArguments>();

            p.Setup(arg => arg.ScriptFile)
                .As('s', "script")
                .Required()
                .WithDescription("Script file.");

            p.Setup(arg => arg.UserName)
                .As('u', "user")
                .WithDescription("Run script against AD Account name.");

            p.Setup(arg => arg.ShowAlert)
                .As('a', "alert")
                .Callback(arg => showUserAlert = arg)
                .SetDefault(false)
                .WithDescription("Notify the user if error. DEFAULT: False");

            p.Setup(arg => arg.HideConsole)
                .As('h', "hide")
                .Callback(arg => hideConsole = arg)
                .SetDefault(false)
                .WithDescription("Hide console window during execution. DEFAULT: False");

            p.SetupHelp("?", "help")
                .Callback(text => Console.WriteLine(text));

            var result = p.Parse(args);

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
                log.Info("Time elapsed: {0:00.00}s", stopwatch.Elapsed.TotalSeconds);
                ShowUserAlert("Command line arguments parsing failed!");
                return -1;
            }

            if (hideConsole)
            {
                ShowWindow(GetConsoleWindow(), SW_HIDE);
                log.Info("Start with console window hidden.");
            }

            AppArguments appArg = p.Object;

            if (!File.Exists(appArg.ScriptFile))
            {
                log.Error("\"{0}\" doesn't exist!", appArg.ScriptFile);
                stopwatch.Stop();
                log.Info("Time elapsed: {0:00.00}s", stopwatch.Elapsed.TotalSeconds);
                ShowUserAlert(string.Format("Missing or wrong script file '{0}'!", appArg.ScriptFile));
                return -2;
            }

            if (!string.IsNullOrEmpty(appArg.UserName))
            {
                user = ActiveDirectoryHelper.FindPrincipal(appArg.UserName);
                if (user == null)
                {
                    stopwatch.Stop();
                    log.Info("Time elapsed: {0:00.00}s", stopwatch.Elapsed.TotalSeconds);
                    log.Error($"Error in resolving provided username: \"{appArg.UserName}\"");
                    return -6;
                }
            }

            if (user != null)
            {
                // Do not map network drives if username argument is present
                // Only check the network shares to be connected for that AD user 
                doMapping = false;
                log.Warn("Checking mappings for \"{0}\".", user.SamAccountName);
                log.Warn("No network drives will be connected.");
            }
            else
            {
                doMapping = true;
                user = UserPrincipal.Current;
                DisconnectAllLocalNetworkDrives();
            }

            groups = ActiveDirectoryHelper.GetMembership(user).ToList();

            Console.WriteLine();

            log.Info("Processing \"{0}\" script ...", appArg.ScriptFile);

            using (StreamReader file = new StreamReader(appArg.ScriptFile))
            {
                script = file.ReadToEnd();
                file.Close();
            }

            var parser = new HikariScriptParser(groups);
            try
            {
                parser.UserName = user.SamAccountName.Trim();
                parser.Parse(script);

                Console.WriteLine();
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
                Task.Delay(500);
                ConnectNetworkDrives(parser.Model);
            }

            stopwatch.Stop();
            log.Info("\nTotal time elapsed: {0:00.00}s", stopwatch.Elapsed.TotalSeconds);
            Console.WriteLine();

            return 0;
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
            log.Warn("There {0} {1} conflict{2} detected.", totalConflicts == 1 ? "is" : "are", totalConflicts, totalConflicts == 1 ? string.Empty : "s");
            Console.WriteLine();
            // A, B and C are taken for sure! =)
            List<string> alpha = "DEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray().Select(c => c + ":").ToList();
            List<string> used = model.UsedDriveLetters();

            // Only in real-run exclude local drives other than A-C
            if (doMapping)
            {
                // All local fixed drives & CD/DVD drives
                List<string> localDrives = DriveInfo.GetDrives().ToList().Where(d => d.DriveType == DriveType.Fixed || d.DriveType == DriveType.CDRom).Select(d => d.Name.Substring(0, 2)).ToList();
                // Add local fixed drives to used drives
                used.AddRange(localDrives);

                log.Info($"Excluding locally found drives: [{string.Join(", ", localDrives.ToArray())}]");
                Console.WriteLine();
            }

            // Generate unused drive letters queue out of all possible excluding used and local fixed drives
            Queue<string> unused = new Queue<string>(alpha.Where(l => !used.Any(c => c == l)));

            // Try moving until resolved or no more available drives
            while (model.TotalConflicts() > 0 && unused.Count > 0)
            {
                string drive = model.GetNextConflictDriveIndex();
                if (!string.IsNullOrWhiteSpace(drive))
                {
                    // Pop 1st available
                    string unusedDrive = unused.Dequeue();
                    model.MoveFirstUNCConflict(drive, unusedDrive);
                }
                if (unused.Count == 0)
                {
                    Console.WriteLine();
                    log.Warn("No more drive letters available!");
                    log.Warn("All unresolved drives will be reduced to the 1st UNC!");
                    model.ReduceUNCs();
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
                    msg = string.Format("{0}\n\nDetails: {1}", msg, message);
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
            bool success = true;
            Forker forker = new Forker();

            foreach (var drive in model.Drives)
            {
                Tuple<string, string> exp_unc = (drive.Value).FirstOrDefault();
                string DriveLetter = drive.Key;
                string expression = exp_unc.Item1;
                string unc = exp_unc.Item2;
                forker.Fork(delegate
                {
                    if (!Directory.Exists(unc))
                    {
                        log.Error($"Network path \"{unc}\" doesn't exist! Drive {DriveLetter} for [{expression}] is NOT connected!");
                        success = false;
                    }
                    else
                    {
                        string result = InvokeWindowsNetworking.connectToRemote(DriveLetter, unc);
                        if (!string.IsNullOrEmpty(result))
                        {
                            log.Error($"Network drive {DriveLetter} -> \"{unc}\" ERROR: {result}");
                            success = false;
                        }
                    }
                });
            }

            forker.Join();

            return success;
        }

        private static void DisconnectAllLocalNetworkDrives()
        {
            Console.WriteLine();
            foreach (DriveInfo networkDrive in DriveInfo.GetDrives().Where(
                (drive) => drive.DriveType == DriveType.Network))
            {
                //Shave the "\" and call drop function.  Note:  Remaps on relogin and doesn't force close.
                string networDriveName = networkDrive.Name.Substring(0, 2);
                //Force disconnect network shares
                int result = InvokeWindowsNetworking.DropNetworkConnection(networDriveName, 0, true);

                switch (result)
                {
                    case InvokeWindowsNetworking.NO_ERROR: //Success
                        log.Info("Disconnect drive - Disconnected: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_BAD_PROFILE:
                        log.Error("Disconnect drive - Bad profile: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_CANNOT_OPEN_PROFILE:
                        log.Error("Disconnect drive - Cannot open profile: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_DEVICE_IN_USE:
                        log.Error("Disconnect drive - Device in use: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_EXTENDED_ERROR:
                        log.Error("Disconnect drive - Extended error: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_NOT_CONNECTED:
                        log.Error("Disconnect drive - Device not connected: {0}", networDriveName);
                        break;
                    case InvokeWindowsNetworking.ERROR_OPEN_FILES:
                        log.Error("Disconnect drive - Error open files: {0}", networDriveName);
                        break;
                    default:
                        log.Error("Disconnect drive - Unknown error code: {0}", networDriveName);
                        break;
                }
            }
            Console.WriteLine();
        }
    }
}
