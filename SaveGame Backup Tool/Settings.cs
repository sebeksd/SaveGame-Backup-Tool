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

using ByteSizeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

// Based on http://stackoverflow.com/a/33917532

namespace SaveGameBackupTool
{
    [Serializable()]
    public class AppSettings
    {
        public int LastSelectedTaskIndex;
        public int ZIP_CompressionLevel = (int)CompressionLevel.Fastest;
        public List<BackupTask> BackupTasks = new List<BackupTask>();

        // window possition settings
        public WindowPosition Window = new WindowPosition();
    }

    public class BackupTaskSettings
    {
        // ## proper default values are set in SetDefaults() and ValidateConfig() ##
        [System.Xml.Serialization.XmlIgnore]
        public int ChangeCounter; // used to check if there was a change recently
        [System.Xml.Serialization.XmlIgnore]
        public PathHelper SourcePathHelper = new PathHelper(); // used to verify source path and to help distinguish file from directory
        [System.Xml.Serialization.XmlIgnore]
        public PathHelper DestinationPathHelper = new PathHelper(); // used to verify destination path and to help distinguish file from directory

        private bool _IsAutoBackupActive;
        private string _Name; // can't be empty

        private string _SavesPath; // path to Game save folder or file
        private string _DestinationPath; // if empty then generated based on SavesPath 
        private int _BackupEvery; // minutes

        private string _BackupFileNamePattern; // use carefully, files should be unique, used in DateTime.Now.ToString so names should be in ""
        private string _FileFilterRegex; // ignored files filter

        private bool _DestinationDirSizeLimitEnabled; // if enabled app will remove old save games to limit destination directory size
        private int _DestinationDirSizeLimit; // size limit for destination directory [MB]
       
        // Getters / Setters
        public bool IsAutoBackupActive { get { return _IsAutoBackupActive; } set { _IsAutoBackupActive = value; ChangeCounter += 1; }}
        
        public string Name { get { return _Name; } set { _Name = value; ChangeCounter += 1; }}

        public string SavesPath { get { return _SavesPath; } set { _SavesPath = value; SourcePathHelper.SetPath(value); ChangeCounter += 1;} }
        public string DestinationPath { get { return _DestinationPath; } set { _DestinationPath = value; DestinationPathHelper.SetPath(value); ChangeCounter += 1; }}
        public int BackupEvery { get { return _BackupEvery; } set { _BackupEvery = value; ChangeCounter += 1; }}

        public string BackupFileNamePattern { get { return _BackupFileNamePattern; } set { _BackupFileNamePattern = value; ChangeCounter += 1; }}
        public string FileFilterRegex { get { return _FileFilterRegex; } set { _FileFilterRegex = value; ChangeCounter += 1; }}

        public bool DestinationDirSizeLimitEnabled { get { return _DestinationDirSizeLimitEnabled; } set { _DestinationDirSizeLimitEnabled = value; ChangeCounter += 1; }}
        public int DestinationDirSizeLimit { get { return _DestinationDirSizeLimit; } set { _DestinationDirSizeLimit = value; ChangeCounter += 1; }}

        public void SetDefault()
        {
            ChangeCounter = 0;
            SourcePathHelper.SetPath("");
            DestinationPathHelper.SetPath("");

            _IsAutoBackupActive = false;

            _Name = "Backup task";

            _SavesPath = ""; 
            _DestinationPath = "";

            _BackupEvery = 5;
            _BackupFileNamePattern = "yyyy.MM.dd-HH.mm.ss"; // use carefully, files should be unique, used in DateTime.Now.ToString
            _FileFilterRegex = @".*\.log|.*\.bak";

            _DestinationDirSizeLimitEnabled = false;
            _DestinationDirSizeLimit = 1000; // [MB]
        }
    }

    public class BackupTask
    {
        public BackupTaskSettings Settings = null;

        [System.Xml.Serialization.XmlIgnore]
        public int ChangeCounter; // used to check if there was a change recently

        private DateTime _LastBackupTime; // last check time, if value is greater then Now it will be automatically set to Now
        private string _LastBackupErrorMessage;
       
        // Getters / Setters
        public DateTime LastBackupTime { get { return _LastBackupTime; } set { _LastBackupTime = value; ChangeCounter += 1; } }
        public string LastBackupErrorMessage { get { return _LastBackupErrorMessage; } set { _LastBackupErrorMessage = value; ChangeCounter += 1; } }

        [System.Xml.Serialization.XmlIgnore]
        public bool BackupChecked = false; // this is only runtime, it says if backup was checked at least once after app started
        [System.Xml.Serialization.XmlIgnore]
        public long DestinationDirectorySize = -1;
        [System.Xml.Serialization.XmlIgnore]
        public long ManualAndPreRestoreBackupsSize = -1;

        [System.Xml.Serialization.XmlIgnore]
        public bool SourceFilesChanged = true; // this flag is used to know if this task require checking files for changes
        [System.Xml.Serialization.XmlIgnore]
        public FileSystemWatcher SourceWatcher = null; // it will throw event every time when file is changed in source path 

        [System.Xml.Serialization.XmlIgnore]
        public bool DestinationDirectoryChanged = true; // this flag is used to know if this task require checking files for changes
        [System.Xml.Serialization.XmlIgnore]
        public FileSystemWatcher DestinationWatcher = null; // it will throw event every time when file is changed in destination path 

        public BackupTask()
        {
            Settings = new BackupTaskSettings();
            SetDefault();
        }

        public void SetDefault()
        {
            Settings.SetDefault();
            
            _LastBackupTime = DateTime.MinValue;
            _LastBackupErrorMessage = "";

            // runtime variables
            ChangeCounter = 0;
            BackupChecked = false;
            DestinationDirectorySize = -1;
            SourceFilesChanged = true; // need to check files on first run
        }

        public void Validate()
        {
            bool lSetDefault = (String.IsNullOrEmpty(Settings.Name)) || (Settings.SourcePathHelper.IsEmpty) || (String.IsNullOrEmpty(Settings.BackupFileNamePattern)) || (Settings.BackupEvery == 0);

            // check if configuration is bad, if it is than set default values
            // - SavesPath if empty then configuration is empty or wrong
            // - BackupFileNamePattern if empty then configuration is empty or wrong
            // - DestinationPath if empty then generated based on SavesPath
            // - FileFilterRegex can be empty, no files will be ignored
            // - BackupEvery must be in range 1-1440 but upper limit is not very important so check only lower limit
            // - LastCheckTime can have any value, if value greater then "Now" it will be automatically converted to "Now" (can occure when PC time was changed)
            if (lSetDefault)
                SetDefault();
            // configuration can be good but last check time is wrong (computer clock was changed ?), correct it
            else if (LastBackupTime > DateTime.Now)
                LastBackupTime = DateTime.Now;

            if (!lSetDefault)
            {
                // if path has no trailing backslash add it (in case user add it manualy to settings XML)
                if (!Settings.DestinationPath.EndsWith("\\"))
                    Settings.DestinationPath += "\\";
            }
        }

        private void OnSourceWatcherTick(object source, FileSystemEventArgs e)
        {
            SourceFilesChanged = true;
        }

        private void OnDestinationWatcherTick(object source, FileSystemEventArgs e)
        {
            DestinationDirectoryChanged = true;
        }

        public void UpdateSourceWatcher()
        {
            if (SourceWatcher == null)
            {
                // create and set basic data
                SourceWatcher = new FileSystemWatcher();
                SourceWatcher.NotifyFilter = NotifyFilters.LastWrite; // write only is enough
                SourceWatcher.InternalBufferSize = 4096; // we don't need large buffer so we are using lowest value
                SourceWatcher.IncludeSubdirectories = true;
                
                SourceWatcher.Changed += new FileSystemEventHandler(OnSourceWatcherTick);
            }

            // set new path to watch
            if (Settings.SourcePathHelper.PathCorrect)
            {
                // if path is pointing to a file we need to split it and use file name as a Filter because watcher can only be pointed to a directory
                SourceWatcher.EnableRaisingEvents = false;

                if (Settings.SourcePathHelper.IsFile)
                    SourceWatcher.Filter = Settings.SourcePathHelper.FileName;
                else
                    SourceWatcher.Filter = "*";

                SourceWatcher.Path = Settings.SourcePathHelper.DirectoryPath;
                SourceWatcher.EnableRaisingEvents = Settings.IsAutoBackupActive; // watch only if task is active
            }
            else
                SourceWatcher.EnableRaisingEvents = false;
        }

        public void UpdateDestinationWatcher()
        {
            if (DestinationWatcher == null)
            {
                // create and set basic data
                DestinationWatcher = new FileSystemWatcher();
                DestinationWatcher.NotifyFilter = NotifyFilters.Size; // size only should be enough
                DestinationWatcher.InternalBufferSize = 4096; // we don't need large buffer so we are using lowest value
                DestinationWatcher.IncludeSubdirectories = false; // we are calculating size only for topmost diretory so no need to Watch for changes in subdirectories
                DestinationWatcher.Filter = "*";

                DestinationWatcher.Changed += new FileSystemEventHandler(OnDestinationWatcherTick);
            }

            // set new path to watch
            if (Directory.Exists(Settings.DestinationPath))
            {
                DestinationWatcher.EnableRaisingEvents = false;

                DestinationWatcher.Path = Settings.DestinationPath;
                DestinationWatcher.EnableRaisingEvents = true; // watch always
            }
            else
                DestinationWatcher.EnableRaisingEvents = false;

            DestinationDirectoryChanged = true; // if Watcher was change we should treat it as change in directry
        }

        public void SetLastBackupStatus(bool lSuccess, string lErrorMessage)
        {
            BackupChecked = true;

            if (lSuccess)
            {
                LastBackupTime = DateTime.Now;
                LastBackupErrorMessage = "";
            }
            else
                LastBackupErrorMessage = lErrorMessage;
        }

        public string GetLastBackupTime()
        {
            if (LastBackupTime == DateTime.MinValue)
                return "[never]";
            else
                return LastBackupTime.ToShortDateString() + " " + LastBackupTime.ToShortTimeString();
        }

        public override string ToString()
        {
            // Using On/Off instead Active/Not active because it is shorten and more important there is smaller difference in length (so its looking better on list)
            string lResult;
            if (Settings.IsAutoBackupActive)
                lResult = " [ On - " + GetLastBackupTime() + " ] ";
            else
                lResult = " [ Off - " + GetLastBackupTime() + " ] ";

            return lResult + Settings.Name;
        }

        public bool DestinationDirectorySizeLimitReached()
        {
            // check if size of directory was readed
            if (DestinationDirectorySize == -1)
                return false;

            return Settings.DestinationDirSizeLimitEnabled && (DestinationDirectorySize > ByteSize.FromMegaBytes(Settings.DestinationDirSizeLimit).Bytes);
        }
    }

    public class WindowPosition
    {
        public bool PositionKnown;
        public double Top;
        public double Left;
        public double Height;
        public double Width;
        public bool Maximised;

        public void SetDefault()
        {
            PositionKnown = false;
            Top = 0;
            Left = 0;
            Height = 0;
            Width = 0;
            Maximised = false;
        }

        public void SetNewValues(double lTop, double lLeft, double lHeight, double lWidth, bool lMaximised)
        {
            PositionKnown = true;
            Top = lTop;
            Left = lLeft;
            Height = lHeight;
            Width = lWidth;
            Maximised = lMaximised;
        }

        public override string ToString()
        {
            return
                "PositionKnown: " + PositionKnown.ToString() + " " +
                "Top: " + Top.ToString() + " " +
                "Left: " + Left.ToString() + " " +
                "Height: " + Height.ToString() + " " +
                "Width: " + Width.ToString() + " " +
                "Maximised: " + Maximised.ToString();
        }
    };

    public class SettingsManager
    {
        private string fSettingFileName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\Settings.xml"; //Path.GetFileNameWithoutExtension(System.AppDomain.CurrentDomain.BaseDirectory) + "Settings.xml";
        private string fSettingTempFileName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\Settings.tmp"; //Path.GetFileNameWithoutExtension(System.AppDomain.CurrentDomain.BaseDirectory) + "Settings.tmp";
        private AppSettings fSettings = new AppSettings();

        public AppSettings Settings
        {
            get { return fSettings; }
            set { fSettings = value; }
        }

        public void SetDefaults()
        {
            fSettings.BackupTasks.Clear();

            // at lest one item must be present
            fSettings.BackupTasks.Add(new BackupTask());

            fSettings.LastSelectedTaskIndex = 0;
            fSettings.ZIP_CompressionLevel = (int)CompressionLevel.Fastest;

            //foreach (BackupTaskSettings lBackupTask in fSettings.BackupTasks)
            //    lBackupTask.SetDefault();

            // window position
            fSettings.Window.SetDefault();
        }

        // Validate config and set defaults
        public void ValidateConfig()
        {
            if (fSettings.ZIP_CompressionLevel < 0 || fSettings.ZIP_CompressionLevel > 9)
                fSettings.ZIP_CompressionLevel = (int)CompressionLevel.Fastest;

            if (fSettings.BackupTasks.Count == 0)
                SetDefaults();
            else
            {
                foreach (var lBackupTask in fSettings.BackupTasks)
                    lBackupTask.Validate();
            }
        }

        // Update all Watchers
        public void UpdateWatchers()
        {
            foreach (var lBackupTask in fSettings.BackupTasks)
            {
                lBackupTask.UpdateSourceWatcher();
                lBackupTask.UpdateDestinationWatcher();
            }
        }

        // Load configuration file
        public void LoadConfig()
        {
            bool lIsFileReadable = false;

            try
            {
                try
                {
                    // before reading check if everything was right during last save
                    if (File.Exists(fSettingFileName))
                    {
                        // check if both files are present, this could mean that save was interrupted during writing to file
                        if (File.Exists(fSettingTempFileName))
                        {
                            // tmp file can be corrupted, remove
                            File.Delete(fSettingTempFileName);
                        }
                    }
                    // check if last save was interrupted during "delete old file"/"rename new file"
                    else if (File.Exists(fSettingTempFileName))
                    {
                        // use tmp file instead of normal, it should be correct
                        File.Move(fSettingTempFileName, fSettingFileName);
                    }
                }
                catch
                { 

                }

                if (File.Exists(fSettingFileName))
                {
                    StreamReader lReader = File.OpenText(fSettingFileName);
                    try
                    {
                        lIsFileReadable = true;

                        Type lType = fSettings.GetType();
                        System.Xml.Serialization.XmlSerializer lSerializer = new System.Xml.Serialization.XmlSerializer(lType);
                        object lData = lSerializer.Deserialize(lReader);
                        fSettings = (AppSettings)lData;
                    }
                    finally
                    {
                        lReader.Close();
                    }
                }
            }
            catch
            {
                // somethink wrong with the file, use default settings
                SetDefaults();

                // if file is readable but its content is bad then rename file to error (so we will not override old file with default data)
                try
                {
                    if (lIsFileReadable)
                        File.Move(fSettingFileName, fSettingFileName + ".error");
                }
                catch
                {

                }
            }

            ValidateConfig();
            UpdateWatchers();
        }

        // Save configuration file
        public void SaveConfig()
        {
            try
            {
                StreamWriter lWriter = File.CreateText(fSettingTempFileName);
                Type lType = fSettings.GetType();
                if (lType.IsSerializable)
                {
                    System.Xml.Serialization.XmlSerializer lSerializer = new System.Xml.Serialization.XmlSerializer(lType);
                    lSerializer.Serialize(lWriter, fSettings);
                    lWriter.Close();
                }

                // save was succesfull, we can remove old file and use new one
                File.Delete(fSettingFileName);
                File.Move(fSettingTempFileName, fSettingFileName);
            }
            catch
            {
                System.Windows.Application.Current.Shutdown();
                System.Windows.MessageBox.Show("Can't save config, please check directory permissions", "Save Game Backup Tool - Critical Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
