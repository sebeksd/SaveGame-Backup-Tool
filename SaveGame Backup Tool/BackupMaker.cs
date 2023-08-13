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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SaveGameBackupTool
{
    public enum BackupType
    {
        btNormal,
        btManual,
        btManualWithCustomPostfix,
        btRestore
    }

    public class BackupMaker
    {
        private readonly CompressionLevel fZIP_CompressionLevel;
        public BackupMaker(CompressionLevel lZIP_CompressionLevel)
        {
            fZIP_CompressionLevel = lZIP_CompressionLevel;
        }

        private string BackupTypeToFilePostfix(BackupType lType, string lManualPostfix)
        {
            switch (lType)
            {
                case BackupType.btManual:
                    return "-manual";
                case BackupType.btRestore:
                    return "-pre_restore";
                case BackupType.btManualWithCustomPostfix:
                    return "-" + lManualPostfix;
                default:
                    return "";
            }
        }

        private string DirectoryForBakupType(BackupType lType, string lDestinationPath)
        {
            string lSubFolder;
            switch (lType)
            {
                case BackupType.btManual:
                case BackupType.btManualWithCustomPostfix:
                    lSubFolder = "Manual";
                    break;
                case BackupType.btRestore:
                    lSubFolder = "Pre-Restore";
                    break;
                default:
                    lSubFolder = "";
                    break;
            }

            if (!String.IsNullOrEmpty(lSubFolder))
                return Path.Combine(lDestinationPath, lSubFolder + "\\");
            else
                return lDestinationPath;
        }

        private bool CheckFilterForFile(string lFileFilterRegex, string lFileName)
        {
            // TODO Performance: make regex object persistent through whole Task scan

            // true if FileName meet Filter
            if (!String.IsNullOrEmpty(lFileFilterRegex))
            {
                Regex lRegexFilter = new Regex(lFileFilterRegex, RegexOptions.IgnoreCase);
                Match lMatch = lRegexFilter.Match(lFileName);

                return lMatch.Success;
            }
            return false;
        }

        private bool IsFileLocked(string lFilePath, ref string pErrorMessage)
        {
            try
            {
                using (Stream stream = new FileStream(lFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    return false;
                }
            }
            catch (Exception E)
            {
                pErrorMessage = E.Message;
                return true;
            }
        }

        public long GetDirectorySize(string lPath)
        {
            try
            {
                DirectoryInfo lDirInfo = new DirectoryInfo(lPath);

                long lTotalSize = 0;
                foreach (FileInfo lFile in lDirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                    lTotalSize += lFile.Length;

                return lTotalSize;
            }
            catch //(Exception E)
            {
                return -1;
            }
        }

        public bool CheckFilesToBackup(BackupTask lBackupTask, ref bool pFileModified, ref bool pFileLocked, ref string pErrorMessage)
        {
            // return on true success, false if error occurred
            //pFileModified = false; // at least one file changed
            //pFileLocked = false; // at least one file locked
            //pErrorMessage = "";
            bool lResult = false;

            if (lBackupTask.Settings.SourcePathHelper.IsNotEmpty)
            {
                try
                {
                    if (!lBackupTask.Settings.SourcePathHelper.ReCheck())
                        throw new ArgumentException("Wrong source directory");

                    // check if we have path to directory or to single file
                    string[] lAllFiles = lBackupTask.Settings.SourcePathHelper.GetAllFiles();

                    foreach (var lFile in lAllFiles)
                    {
                        // ignore log files
                        if (!CheckFilterForFile(lBackupTask.Settings.FileFilterRegex, lFile))
                        {
                            // check every file to find if any of them are locked
                            if (IsFileLocked(lFile, ref pErrorMessage))
                            {
                                pFileLocked = true;
                                break;
                            }
                            // check if file was modified, do that until first modified file is found
                            else if (!pFileModified)
                            {
                                FileInfo lInfo = new FileInfo(lFile);

                                if (lInfo.LastWriteTime > lBackupTask.LastBackupTime)
                                    pFileModified = true;
                            }
                        }
                    }

                    // if file locked then this is error else success
                    // to consider: check both FileLocked and FileModified simultaneously and return error only if is Modified and is Locked instead if it is only Locked (in other words report error only if files did change but they are locked)
                    lResult = !pFileLocked;
                }
                catch (Exception E)
                {
                    pErrorMessage = E.Message;
                }
            }

            return lResult;
        }

        public bool CheckBackupFrequency(BackupTask lBackupTask)
        {
            // function check if frequency + last backup time passed and next backup should be made
            // if last backup was unsuccessful then we will return true (checking this task every tick)
            DateTime lNextCheckTime = lBackupTask.LastBackupTime.AddMinutes(lBackupTask.Settings.BackupEvery);

            return (lNextCheckTime < DateTime.Now);
        }

        public void RefreshDestinationDirectorySize(BackupTask pBackupTask)
        {
            pBackupTask.DestinationDirectorySize = GetDirectorySize(pBackupTask.Settings.DestinationPath);

            long lManualSize = GetDirectorySize(Path.Combine(pBackupTask.Settings.DestinationPath, "Manual"));
            long lPreRestore = GetDirectorySize(Path.Combine(pBackupTask.Settings.DestinationPath, "Pre-Restore"));

            pBackupTask.ManualAndPreRestoreBackupsSize = (lManualSize > 0 ? lManualSize: 0) + (lPreRestore > 0 ? lPreRestore : 0);
        }

        public void CleanUpOldBackups(BackupTask lBackupTask, ref string pErrorMessage)
        {
            if (lBackupTask.DestinationDirectorySizeLimitReached())
            {
                DirectoryInfo lDirInfo = new DirectoryInfo(lBackupTask.Settings.DestinationPath);
                FileInfo[] lFiles = lDirInfo.GetFiles().OrderBy(f => f.CreationTime).ToArray();
                try
                {
                    int lFilesCount = lFiles.Length;

                    foreach (FileInfo lFile in lFiles)
                    {
                        // even if limit is reached we leave at least 4 files
                        if (lFilesCount <= 4)
                            return;

                        // remove current file
                        lFile.Delete();
                        lFilesCount--;

                        RefreshDestinationDirectorySize(lBackupTask);

                        if (!lBackupTask.DestinationDirectorySizeLimitReached())
                            break;
                    }
                }
                catch (Exception E)
                {
                    pErrorMessage = E.Message;
                }
            }
        }

        public bool MakeBackup(BackupTask lBackupTask, BackupType lType, ref string pErrorMessage)
        {
            return MakeBackup(lBackupTask, lType, "", ref pErrorMessage);
        }

        public bool MakeBackup(BackupTask lBackupTask, BackupType lType, string lManualPostfix, ref string pErrorMessage)
        {
            string lBackupFileName = DateTime.Now.ToString(lBackupTask.Settings.BackupFileNamePattern);
            lBackupFileName += BackupTypeToFilePostfix(lType, lManualPostfix);
            lBackupFileName += ".zip";
            string lDestinationPath = DirectoryForBakupType(lType, lBackupTask.Settings.DestinationPath);

            try
            {
                // to be sure that backup dir exists
                Directory.CreateDirectory(lDestinationPath);

                // try to make a backup
                if (lBackupTask.Settings.SourcePathHelper.IsFile)
                    ZipHelper.CreateFromFile(lBackupTask.Settings.SourcePathHelper.FilePath, lDestinationPath + lBackupFileName, fZIP_CompressionLevel, false, null, null);
                else
                    ZipHelper.CreateFromDirectory(lBackupTask.Settings.SourcePathHelper.DirectoryPath, lDestinationPath + lBackupFileName, fZIP_CompressionLevel, false, null, lFilterFileName => !CheckFilterForFile(lBackupTask.Settings.FileFilterRegex, lFilterFileName));
                
                pErrorMessage = "";

                return true;
            }
            catch (Exception E)
            {
                pErrorMessage = E.Message;

                try
                {
                    // probably file was created but content is corrupted or just not complete, remove it
                    File.Delete(lDestinationPath + lBackupFileName);
                }
                catch
                {
                    // not important
                }
                return false;
            }
        }

        public string[] GetBackupsList(BackupTask lBackupTask, ref string pErrorMessage)
        {
            // return list of files
            // pErrorMessage = "";
            if (lBackupTask.Settings.DestinationPathHelper.IsNotEmpty)
            {
                try
                {
                    return lBackupTask.Settings.DestinationPathHelper.GetAllFiles();
                }
                catch (Exception E)
                {
                    pErrorMessage = E.Message;
                }
            }

            return null;
        }

        public bool RestoreBackup(BackupTask lBackupTask, string lBackupFilePath)
        {
            if (lBackupTask.Settings.SourcePathHelper.PathCorrect && lBackupTask.Settings.DestinationPathHelper.PathCorrect)
            {
                bool lFileModified = false; // TODO maybe warning on restore that files changed
                bool lFileLocked = false;
                string lErrorMessage = "";

                // some file is locked, stop restore
                if (CheckFilesToBackup(lBackupTask, ref lFileModified, ref lFileLocked, ref lErrorMessage) && !lFileLocked)
                {
                    try
                    {
                        ZipHelper.ExtractToDirectory(lBackupFilePath, lBackupTask.Settings.SourcePathHelper.DirectoryPath);
                        return true;
                    }
                    catch
                    { 
                    }
                }
                else
                {
                    // TODO error message
                }
            }

            return false;
        }
    }
}
