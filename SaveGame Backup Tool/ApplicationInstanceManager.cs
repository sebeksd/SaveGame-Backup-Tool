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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SaveGameBackupTool
{
    // Based on http://stackoverflow.com/a/184143
    // this class is for finding if application is running and also to refocuse on previous instance
    class ApplicationInstanceManager
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private System.Threading.Mutex fMutex = null; // Mutex used to chcek if application is running only in one instance

        /// <summary>
        /// This class is for finding if application is already running, also it will refocuse on previous instance then exit current one.
        /// Object must exist whole time we need to lock other instances from running, to allow other instances just destroy this object
        /// </summary>
        /// <param name="lUniqueName">Manager will check if application is running by this parameter, if you live it empty default one will be used, it is recommended to use a GUID</param>
        public ApplicationInstanceManager(string lUniqueIdentifier = "")
        {
            bool lMutexCreated = false;

            string lMutexName = lUniqueIdentifier;
            if (string.IsNullOrEmpty(lMutexName))
                lMutexName = @"{B1DBC8F1-907F-4552-B51D-10172D6CAD02}";

            fMutex = new System.Threading.Mutex(true, lMutexName, out lMutexCreated);

            if (!lMutexCreated)
            {
                FocuseOnOldInstance();
                Environment.Exit(0);
            }
        }

        ~ApplicationInstanceManager()
        {
            if (fMutex != null)
                fMutex.Close();
        }
       
        private void FocuseOnOldInstance()
        {
            Process lCurrent = Process.GetCurrentProcess();
            foreach (Process lItem in Process.GetProcessesByName(lCurrent.ProcessName))
            {
                if (lItem.Id != lCurrent.Id)
                {
                    // maybe better do it on Messages with will also show app if it is in tray
                    SetForegroundWindow(lItem.MainWindowHandle);
                    break;
                }
            }
        }
    }
}
