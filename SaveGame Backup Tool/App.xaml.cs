/*
   SaveGame Backup Tool -  Application for automatic Games Saves backup
   Copyright (C) 2018 sebeksd

   This program is free software; you can redistribute it and/or modify
   it under the terms of the GNU Lesser General Public License as published by
   the Free Software Foundation; either version 3 of the License, or
   (at your option) any later version.
   
   This program is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
   GNU Lesser General Public License for more details.

   You should have received a copy of the GNU Lesser General Public License
   along with this program; if not, write to the Free Software Foundation,
   Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301  USA
*/

using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SaveGameBackupTool
{
    public partial class App : Application
    {
        public static CommandLineParameters fCommandLineOptions = null;

        [DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            bool lShowHelp = false;

            fCommandLineOptions = new CommandLineParameters();
            if (CommandLine.Parser.Default.ParseArguments(e.Args, fCommandLineOptions))
            {
                if (!String.IsNullOrEmpty(fCommandLineOptions.DoBackup))
                {
                    if (fCommandLineOptions.DoBackup.StartsWith("="))
                        fCommandLineOptions.DoBackup = fCommandLineOptions.DoBackup.Remove(0, 1);
                    
                    // user whant to make quick backup by Task Name
                    SettingsManager lSettings = new SettingsManager();
                    lSettings.LoadConfig();

                    AttachConsole(-1);

                    bool lFound = false;
                    foreach (BackupTask lBackupTask in lSettings.Settings.BackupTasks)
                    {
                        if (lBackupTask.Settings.Name == fCommandLineOptions.DoBackup)
                        {
                            lFound = true;

                            // do backup
                            BackupMaker lBackupMaker = new BackupMaker();
                            string lErrorMessage = "";

                            if (lBackupMaker.MakeBackup(lBackupTask, false, ref lErrorMessage))
                                Console.WriteLine("SUCCEED: Task '" + lBackupTask.Settings.Name + "' backup created");
                            else
                                Console.WriteLine("FAILED: Task '" + lBackupTask.Settings.Name + "' backup failed: \r\n" + lErrorMessage);
                            break;
                        }
                    }

                    if (!lFound)
                        Console.WriteLine("FAILED: Task '" + fCommandLineOptions.DoBackup + "' not found");

                    Environment.Exit(1);
                }
                else
                {
                    // ok, nothing more to do, parameters will be used in main app
                }

                // check if option -h, -help was used
                lShowHelp = fCommandLineOptions.ShowHelp;
            }
            else
            {
                //MessageBox.Show("Bad args!");
                lShowHelp = true;
            }

            // display help info in case of bad arguments or -h, -help options
            if (lShowHelp)
            {
                AttachConsole(-1);
                Console.WriteLine(fCommandLineOptions.GetUsage());
                Environment.Exit(1);
            }
        }
    }
}
