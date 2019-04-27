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

using CommandLine;
using CommandLine.Text;

namespace SaveGameBackupTool
{
    public class CommandLineParameters
    {
        [Option('t', "tray", Required = false,
          HelpText = "Run application in tray (also don't show on taskbar)")]
        public bool StartInTray { get; set; }

        [Option('b', "backup", Required = false,
          HelpText = "Find backup task with this name and do quick manual backup now. Usage -b=ABC")]
        public string DoBackup { get; set; }

        [Option('h', "help", Required = false,
          HelpText = "Dispaly this help screen.")]
        public bool ShowHelp { get; set; }
        
        public string GetUsage()
        {
            return "\r\n\r\n" + HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
