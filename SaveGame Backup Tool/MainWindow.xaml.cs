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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Timers;
using System.Windows.Threading;
using System.ComponentModel;
using Hardcodet.Wpf.TaskbarNotification;
using ByteSizeLib;
using System.IO;
using Microsoft.Win32;

namespace SaveGameBackupTool
{
    public partial class MainWindow : Window
    {
        private bool fGUI_Reload = true; // eg. used to skip GUI controls events triggered by loading settings values to them
        private bool fTasksCheckedAtLeastOnce = false;
        Timer fTimer;
        SettingsManager fSettings;

        int fSelectedBackupTaskIndex = -1;

        TaskbarIcon fTray = null; // it is created if app should run in tray
        ApplicationInstanceManager fApplicationInstanceManager = null; // this class is used to determine if application is runnig in only one instance

        BackupMaker fBackupMaker = null; // this class has all stuff related to making backup and checking files

        About fAbout = null;
        ContextMenu fTypicalLocationsMenu = null;
        Int64 fSettingsChangeCounter = -1; // this value is used to find if there was a change in settings 

        Restore fRestore = null;

        private void OpenDirectoryInExplorer(string lPath)
        {
            try
            {
                //System.Diagnostics.Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)))
                System.Diagnostics.Process.Start(Environment.ExpandEnvironmentVariables(lPath));
            }
            catch
            {
            }
        }
         
        private void CreateTypicalLocationsMenu()
        {
            fTypicalLocationsMenu = new ContextMenu();

            MenuItem lAppDataItem = new MenuItem();
            lAppDataItem.Header = @"AppData";
            lAppDataItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(@"%AppData%"));
            fTypicalLocationsMenu.Items.Add(lAppDataItem);

            MenuItem lMySavesItem = new MenuItem();
            lMySavesItem.Header = @"UserProfile\My Saves";
            lMySavesItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(@"%UserProfile%\Saved Games"));
            fTypicalLocationsMenu.Items.Add(lMySavesItem);

            // path to "My Documents" can be changed, if so then path %UserProfile%\Documents will point to Default (old) location, in that case we want to show both locations in menu because some games 
            // can write to old location
            string lMyDocumentsPathReal = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", "Personal", @"%UserProfile%\Documents")?.ToString();
            string lMyDocumentsPathDefault = Environment.ExpandEnvironmentVariables(@"%UserProfile%\Documents");

            MenuItem lMyDocumentsItem = new MenuItem();
            lMyDocumentsItem.Header = @"My Documents";
            lMyDocumentsItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(lMyDocumentsPathReal));
            fTypicalLocationsMenu.Items.Add(lMyDocumentsItem);

            if (lMyDocumentsPathReal != lMyDocumentsPathDefault)
            {
                MenuItem lDefaultMyDocumentsItem = new MenuItem();
                lDefaultMyDocumentsItem.Header = @"Old My Documents";
                lDefaultMyDocumentsItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(lMyDocumentsPathDefault));
                fTypicalLocationsMenu.Items.Add(lDefaultMyDocumentsItem);
            }

            MenuItem lMyGamesItem = new MenuItem();
            lMyGamesItem.Header = @"My Games";
            lMyGamesItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(lMyDocumentsPathReal + @"\My Games"));
            fTypicalLocationsMenu.Items.Add(lMyGamesItem);

            string lSteamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "")?.ToString();
            if (Directory.Exists(lSteamPath))
            {
                MenuItem lSteamItem = new MenuItem();
                lSteamItem.Header = @"Steam Library";
                lSteamItem.Click += new RoutedEventHandler((sender, e) => OpenDirectoryInExplorer(lSteamPath));
                fTypicalLocationsMenu.Items.Add(lSteamItem);
            }

            ButtonTypicalLocations.ContextMenu = fTypicalLocationsMenu;
        }

        public MainWindow()
        {
            InitializeComponent();

            // check if application is running only in one instance if not then focuse on old instance and exit
            fApplicationInstanceManager = new ApplicationInstanceManager();
            CreateTypicalLocationsMenu();
            

            fBackupMaker = new BackupMaker();

            fSettings = new SettingsManager();
            fSettings.LoadConfig();

            // set window position
            if (fSettings.Settings.Window.PositionKnown)
            {
                this.WindowState = WindowState.Normal;
                this.Top = fSettings.Settings.Window.Top;
                this.Left = fSettings.Settings.Window.Left;
                this.Height = fSettings.Settings.Window.Height;
                this.Width = fSettings.Settings.Window.Width;

                // done in event 'Window_Loaded()' because window must finish its initialization before setting this flag
                //if (fSettings.Settings.WindowMaximised)
                //    WindowState = WindowState.Maximized;
            }

            // if app is set up to run in tray than create tray icon and minimize
            if (App.fCommandLineOptions.StartInTray)
            {
                ShowInTaskbar = false;

                fTray = new TaskbarIcon();
                //fTray.Icon = Properties.Resources.TrayIcon; // UpdateGlobalStatus will set correct Icon
                fTray.ToolTipText = this.Title;
                fTray.TrayMouseDoubleClick += (EventSender, EventArgs) => { Show(); WindowState = WindowState.Normal; };

                fTray.ContextMenu = new ContextMenu();
                MenuItem lCloseMenuItem = new MenuItem();
                lCloseMenuItem.Header = "Close";
                lCloseMenuItem.Click += (EventSender, EventArgs) => { Application.Current.Shutdown(); };
                fTray.ContextMenu.Items.Add(lCloseMenuItem);
                fTray.MenuActivation = PopupActivationMode.RightClick;

                WindowState = WindowState.Minimized;
                Hide();
            }

            // referesh destination directory size
            foreach (BackupTask lBackupTask in fSettings.Settings.BackupTasks)
            {
                if (lBackupTask.DestinationDirectorySize == -1)
                    fBackupMaker.RefreshDestinationDirectorySize(lBackupTask);
            }

            FillComboBoxItems();
            SelectTaskByIndex(fSettings.Settings.LastSelectedTaskIndex, false);

            // fill components tooltips
            SetToolTips();

            fTimer = new Timer(10000);
            fTimer.Elapsed += HandleTimer;
            fTimer.AutoReset = true;
            fTimer.Start();
            fTimer.Enabled = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // we need to do it here because maximizing in constructor maximize window on Mian monitor/display and not on "location" monitor
            if (fSettings.Settings.Window.PositionKnown && fSettings.Settings.Window.Maximised && !App.fCommandLineOptions.StartInTray)
                WindowState = WindowState.Maximized;
        }

        private void SetToolTips()
        {
            const int cToolTipDuration = 15000;
            ToolTipService.SetShowDuration(textBoxTaskName, cToolTipDuration);
            ToolTipService.SetShowDuration(textBoxSavesPath, cToolTipDuration);
            ToolTipService.SetShowDuration(textBoxDestinationPath, cToolTipDuration);
            ToolTipService.SetShowDuration(textBoxFileNameFilter, cToolTipDuration);
            ToolTipService.SetShowDuration(textBoxBackupFileNamePattern, cToolTipDuration);
            ToolTipService.SetShowDuration(checkBoxAutomaticBackup, cToolTipDuration);
            ToolTipService.SetShowDuration(buttonForceBackup, cToolTipDuration);
            ToolTipService.SetShowDuration(decimalUpDownBackupEvery, cToolTipDuration);
            ToolTipService.SetShowDuration(rectangleStatusIcon, cToolTipDuration);
            ToolTipService.SetShowDuration(checkBoxAutomaticDestinationDirSizeLimit, cToolTipDuration);
            ToolTipService.SetShowDuration(decimalUpDownDestinationDirSizeLimit, cToolTipDuration);

            textBoxTaskName.ToolTip = "Name of task, mainly used to find task on list";
            textBoxSavesPath.ToolTip = "Path to directory or single file that you want to backup (you can set automatic backup or use manual one)";
            textBoxDestinationPath.ToolTip = "Path to directory where backup ZIP files will be stored, it will create directory if it does not exists.\r\n" +
                "If you like default one leave it empty (app will create 'SaveBackup' directory one level up from saves path)";
            textBoxFileNameFilter.ToolTip = "When task is triggered it will check file or files (depending on which one was selected, file or directory)\r\n" +
                "if one of them was modified (and not locked for reading), this regex will filter files that you want to ignore.\r\n\r\n" +
                "Files will be ignored in scan and in final backup";
            textBoxBackupFileNamePattern.ToolTip = "Backup create new ZIP file, this pattern is used to name this result file.\r\n\r\n" +
                "Remember to set pattern the way that file name will be unique (eg time),\r\n" +
                "function to create name from the pattern is DateTime.ToString for more info about this pattern possibilities search Internet :)";
            checkBoxAutomaticBackup.ToolTip = "Check if you want this tusk to be run every few minutes (backup will be made only if files were modified).";
            buttonForceBackup.ToolTip = "Perform a backup regardless of whether the files have been modified, if files are locked it will fail.";
            decimalUpDownBackupEvery.ToolTip = "Task will launch every few minutes (value set in this field) and it will check if file was modified and also if file is not locked.\r\n" +
                "If file was not modified from last backup or it is locked for reading then next check will be triggered every 30 seconds (till backup is made).";
            rectangleStatusIcon.ToolTip = "Global tasks status (works only for automated tasks), blue - tasks not checked, green - all tasks succeed or nothing to do, red - some task failed.\r\n" +
                "In tray icon mode this status is also presented by icon in tray.";

            checkBoxAutomaticDestinationDirSizeLimit.ToolTip = "If this option is checked application will cleanup older files/backups from destination directory to maintain size limit set in by user (at least 4 last files remains).\r\n" +
                "WARNING! Make sure destination directory is used only by one task/game because files are removed by creation date and file names are not used to chcek if file is from different task!";
            decimalUpDownDestinationDirSizeLimit.ToolTip = "Limit value in [MB] for automated deletion of older files/backups from destination directory (at least 4 last files remains).\r\n" +
                "WARNING! Make sure destination directory is used only by one task/game because files are removed by creation date and file names are not used to chcek if file is from different task!";
        }

        private void UpdateGlobalStatus(bool lSuccess)
        {
            // update tray icon if created
            if (fTray != null)
            {
                if (!fTasksCheckedAtLeastOnce)
                    fTray.Icon = Properties.Resources.TrayIcon;
                else if (lSuccess)
                    fTray.Icon = Properties.Resources.OkIcon;
                else
                    fTray.Icon = Properties.Resources.ErrorIcon;
            }

            // update status color
            if (!fTasksCheckedAtLeastOnce)
                rectangleStatusIcon.Fill = new SolidColorBrush(Color.FromRgb(136, 207, 255));
            else if (lSuccess)
                rectangleStatusIcon.Fill = new SolidColorBrush(Color.FromRgb(140, 191, 38));
            else
                rectangleStatusIcon.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 140));
        }

        private void FillComboBoxItems()
        {
            bool lAllSuccess = true;

            comboBoxBackupTasks.Items.Clear();
            foreach (BackupTask lBackupTask in fSettings.Settings.BackupTasks)
            {                
                bool lTaskSuccess = String.IsNullOrEmpty(lBackupTask.LastBackupErrorMessage);

                ComboBoxItem lItem = new ComboBoxItem();
                lItem.Content = lBackupTask;
                lItem.Foreground = GetBackupStatusColorForText(lBackupTask.BackupChecked, lTaskSuccess);
                comboBoxBackupTasks.Items.Add(lItem);

                // global status only for autobackup
                if (lBackupTask.Settings.IsAutoBackupActive)
                {
                    if (!lTaskSuccess)
                        lAllSuccess = false;
                }
            }

            UpdateGlobalStatus(lAllSuccess);
        }

        private void SelectTaskByIndex(int lIndex, bool lComboBoxOnly)
        {
            // set lIndex to -1 to reselect item previously selected
            if ((lIndex == -1) && (fSelectedBackupTaskIndex > -1))
                lIndex = fSelectedBackupTaskIndex;

            if (lIndex >= fSettings.Settings.BackupTasks.Count)
                lIndex = 0;

            if ((lIndex >= 0) && (lIndex < fSettings.Settings.BackupTasks.Count))
            {
                fGUI_Reload = true;
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[lIndex];
                fSelectedBackupTaskIndex = lIndex;

                comboBoxBackupTasks.SelectedIndex = lIndex;
                fSettings.Settings.LastSelectedTaskIndex = lIndex;

                // check if we want to refresh all or just reselect combobox
                if (!lComboBoxOnly)
                {
                    checkBoxAutomaticBackup.IsChecked = lBackupTask.Settings.IsAutoBackupActive;

                    textBoxTaskName.Text = lBackupTask.Settings.Name;
                    textBoxSavesPath.Text = lBackupTask.Settings.SavesPath;
                    textBoxDestinationPath.Text = lBackupTask.Settings.DestinationPath;

                    textBoxFileNameFilter.Text = lBackupTask.Settings.FileFilterRegex;
                    textBoxBackupFileNamePattern.Text = lBackupTask.Settings.BackupFileNamePattern;

                    if (lBackupTask.DestinationDirectorySize > 0)
                        labelDestinationDirSizeValue.Content = ByteSize.FromBytes(lBackupTask.DestinationDirectorySize).ToString();
                    else if (lBackupTask.DestinationDirectorySize == 0)
                        labelDestinationDirSizeValue.Content = "[empty]";
                    else
                        labelDestinationDirSizeValue.Content = "[unknown]";

                    // if directory was not set (eg. older version of app) set it to default one
                    if (lBackupTask.Settings.DestinationPath.Length == 0)
                        lBackupTask.Settings.DestinationPath = GetDefaultDestinationPath(lBackupTask.Settings.SavesPath);

                    checkBoxAutomaticDestinationDirSizeLimit.IsChecked = lBackupTask.Settings.DestinationDirSizeLimitEnabled;
                    decimalUpDownDestinationDirSizeLimit.Value = lBackupTask.Settings.DestinationDirSizeLimit;

                    decimalUpDownBackupEvery.Value = lBackupTask.Settings.BackupEvery;
                    SetMakeBackupStatus(lBackupTask.BackupChecked, String.IsNullOrEmpty(lBackupTask.LastBackupErrorMessage));

                    // set last backup time
                    labelLastBackupValue.Content = lBackupTask.GetLastBackupTime();

                    if (String.IsNullOrEmpty(lBackupTask.LastBackupErrorMessage))
                        textBlockLastError.Text = "none";
                    else
                        textBlockLastError.Text = lBackupTask.LastBackupErrorMessage;
                }
                fGUI_Reload = false;
            }
        }

        private string GetDefaultDestinationPath(string lSavesDirectory)
        {
            // defualt destination path (one directory up)
            if (lSavesDirectory.Length > 0)
                return Directory.GetParent(lSavesDirectory) + "\\SaveBackup\\";
            else
                return "";
        }

        private void buttonGetSavesPathFile_Click(object sender, RoutedEventArgs e)
        {
            Gat.Controls.OpenDialogView lOpenDialog = new Gat.Controls.OpenDialogView();
            Gat.Controls.OpenDialogViewModel lOpenDialogVM = (Gat.Controls.OpenDialogViewModel)lOpenDialog.DataContext;

            lOpenDialogVM.IsDirectoryChooser = false;

            if (lOpenDialogVM.Show() == true)
            {
                textBoxSavesPath.Text = lOpenDialogVM.SelectedFilePath;
                checkBoxAutomaticBackup.IsChecked = true;
            }
        }

        private void checkBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.IsAutoBackupActive = (checkBoxAutomaticBackup.IsChecked == true);
                lBackupTask.UpdateSourceWatcher();

                // status change, refill combobox to refresh names
                FillComboBoxItems();
                SelectTaskByIndex(-1, true); // reselect same item
            }
        }

        private SolidColorBrush GetBackupStatusColorForText(bool lBackupChecked, bool lBackupStatus)
        {
            if (!lBackupChecked)
                return Brushes.Black;
            else if (lBackupStatus)
                return Brushes.Green;
            else
                return Brushes.Red;
        }

        private void SetMakeBackupStatus(bool lBackupChecked, bool lBackupStatus)
        {
            labelLastBackupValue.Foreground = GetBackupStatusColorForText(lBackupChecked, lBackupStatus);
        }

        private void CheckTasks()
        {
            bool lRefreshComboBox = false;
            Int64 lSettingsChangeCounter = 0;

            for (int lIndex = 0; lIndex < fSettings.Settings.BackupTasks.Count; ++lIndex)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[lIndex];
                bool lItemUpdateNeeded = false;

                // calculate global ChangeCount of all task, it will be used to see if there was some change made recently
                // we calculate ChangeCount only for parameters that we want to check
                lSettingsChangeCounter += lBackupTask.ChangeCounter + lBackupTask.Settings.ChangeCounter;

                // if paths are wrong we can't do anything, go to next task 
                if (lBackupTask.Settings.SourcePathHelper.IsEmpty || String.IsNullOrEmpty(lBackupTask.Settings.DestinationPath))
                    continue;

                // check flag from SourceFileWatcher
                if (lBackupTask.SourceFilesChanged)
                {
                    // if auto backup enabled and check frequency
                    if (lBackupTask.Settings.IsAutoBackupActive && fBackupMaker.CheckBackupFrequency(lBackupTask))
                    {
                        string lErrorMessage = "";
                        bool lErrorOccurred = false;

                        bool lFileWasModified = false;
                        bool lFileIsLocked = false;

                        // we will do something, anything happens it GUI need to be updated
                        lItemUpdateNeeded = true;

                        // check for files modification, function result success/error
                        if (!fBackupMaker.CheckFilesToBackup(lBackupTask, ref lFileWasModified, ref lFileIsLocked, ref lErrorMessage))
                        {
                            // error occurred (or file is locked)
                            lRefreshComboBox = true;
                            lErrorOccurred = true;
                            lBackupTask.SetLastBackupStatus(!lErrorOccurred, lErrorMessage);
                        }
                        else if (lFileWasModified)
                        {
                            // success and file was modified
                            lErrorOccurred = !fBackupMaker.MakeBackup(lBackupTask, false, ref lErrorMessage);
                            lBackupTask.SetLastBackupStatus(!lErrorOccurred, lErrorMessage);
                            lRefreshComboBox = true;
                        }
                        else
                        {
                            // no error (and files are not locked) but also nothing changed
                            if (!String.IsNullOrEmpty(lBackupTask.LastBackupErrorMessage))
                            {
                                lBackupTask.LastBackupErrorMessage = "";
                                lRefreshComboBox = true;
                            }
                        }

                        // backup was updated or there was no change that is important for us
                        // if error then try again latter (do not reset FilesChanged flag)
                        if (!lErrorOccurred)
                            lBackupTask.SourceFilesChanged = false;
                    }
                }

                // check flag from DestinationFileWatcher
                if (lBackupTask.DestinationDirectoryChanged)
                {
                    string lErrorMessage = "";
                    // to check if Size was changed
                    long lLastDestinationDirectorySize = lBackupTask.DestinationDirectorySize;

                    // backup was made so we need to update destination directory size once more
                    fBackupMaker.RefreshDestinationDirectorySize(lBackupTask);
                    // if destination directory size limit is set then remove older files
                    fBackupMaker.CleanUpOldBackups(lBackupTask, ref lErrorMessage);
            
                    lBackupTask.DestinationDirectoryChanged = false;

                    // if size is same as before skip updating
                    lItemUpdateNeeded = (lLastDestinationDirectorySize != lBackupTask.DestinationDirectorySize);
                }

                // if we made a backup of currently selected item we need to refresh data on GUI
                if (fSelectedBackupTaskIndex == lIndex)
                {
                    // check if something change or maybe we updated destination directory size
                    if (lItemUpdateNeeded)
                        SelectTaskByIndex(lIndex, false); // reselect same item, refresh interface
                }
            }

            fTasksCheckedAtLeastOnce = true;

            // Last Backup Time of some item changed, refresh list
            if (lRefreshComboBox)
                FillComboBoxItems();

            // save settings if something changed
            if (fSettingsChangeCounter != lSettingsChangeCounter)
            {
                if (fSettingsChangeCounter != -1)
                    fSettings.SaveConfig();
                fSettingsChangeCounter = lSettingsChangeCounter;
            }
        }

        private void HandleTimer(Object source, ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                // go through all taskas and check if there is something to do
                CheckTasks();
            }));
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            // remember windows state
            if ((WindowState == WindowState.Maximized) || (WindowState == WindowState.Minimized))
            {
                if (!this.RestoreBounds.IsEmpty)
                    // get values from RestoreBounds because window is minimized or maximized
                    fSettings.Settings.Window.SetNewValues(this.RestoreBounds.Top, this.RestoreBounds.Left, this.RestoreBounds.Height, this.RestoreBounds.Width, (WindowState == WindowState.Maximized));
                else
                {
                    // window is minimized or maximized but RestoreBounds are empty, leave previous values
                    // this probably happens when window is created minimized and never goes to normal
                }
            }
            else
                // get values directly from window
                fSettings.Settings.Window.SetNewValues(this.Top, this.Left, this.Height, this.Width, false);

            // write current session data
            fSettings.SaveConfig();
        }

        private void textBoxSavesDirectory_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];
                lBackupTask.Settings.SavesPath = textBoxSavesPath.Text;
                lBackupTask.UpdateSourceWatcher();
                // set DestinationPath only if app is running and value of "textBoxSavesFolder" change
                // don't change DestinationPath if it is already set (e.g manually)
                if (lBackupTask.Settings.DestinationPath.Length == 0)
                    lBackupTask.Settings.DestinationPath = GetDefaultDestinationPath(lBackupTask.Settings.SavesPath);
            }

            // simple validation
            if (Directory.Exists(textBoxSavesPath.Text) || File.Exists(textBoxSavesPath.Text))
                textBoxSavesPath.BorderBrush = Brushes.Black;
            else
                textBoxSavesPath.BorderBrush = Brushes.Red;
        }   

        private void textBoxDestinationPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                if (textBoxDestinationPath.Text.Length > 0)
                {
                    // if path has no trailing backslash add it
                    if (!textBoxDestinationPath.Text.EndsWith("\\"))
                        textBoxDestinationPath.Text += "\\";

                    lBackupTask.Settings.DestinationPath = textBoxDestinationPath.Text;
                }
                else
                {
                    lBackupTask.Settings.DestinationPath = GetDefaultDestinationPath(lBackupTask.Settings.SavesPath);
                }

                lBackupTask.UpdateDestinationWatcher();
            }
        }

        private void decimalUpDownBackupEvery_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];
                lBackupTask.Settings.BackupEvery = (int)decimalUpDownBackupEvery.Value;
            }
        }

        private void buttonForceBackup_Click(object sender, RoutedEventArgs e)
        {
            BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

            string lErrorMessage = "";
            lBackupTask.SetLastBackupStatus(fBackupMaker.MakeBackup(lBackupTask, true, ref lErrorMessage), lErrorMessage);

            // we made backup of currently selected item, refresh GUI to refresh last backup time
            SelectTaskByIndex(-1, false); // reselect same item, refresh interface
        }

        private void buttonAddBackupTask_Click(object sender, RoutedEventArgs e)
        {
            BackupTask lBackupTask = new BackupTask();
            lBackupTask.SetDefault();

            fSettings.Settings.BackupTasks.Add(lBackupTask);

            FillComboBoxItems();
            SelectTaskByIndex(fSettings.Settings.BackupTasks.Count - 1, false);
        }

        private void comboBoxBackupTasks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectTaskByIndex(comboBoxBackupTasks.SelectedIndex, false);
        }

        private void buttonRemoveBackupTask_Click(object sender, RoutedEventArgs e)
        {
            if ((fSettings.Settings.BackupTasks.Count > 1) && YesNoQuestion("Are you sure you want to delete current task?"))
            {
                fSettings.Settings.BackupTasks.RemoveAt(fSelectedBackupTaskIndex);
                fSelectedBackupTaskIndex = 0;

                FillComboBoxItems();
                SelectTaskByIndex(-1, false);
            }
        }

        private bool YesNoQuestion(string lQuestion)
        {
            return (MessageBox.Show(lQuestion, this.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        }

        private void buttonGetDestinatinPath_Click(object sender, RoutedEventArgs e)
        {
            Gat.Controls.OpenDialogView lOpenDialog = new Gat.Controls.OpenDialogView();
            Gat.Controls.OpenDialogViewModel lOpenDialogVM = (Gat.Controls.OpenDialogViewModel)lOpenDialog.DataContext;

            lOpenDialogVM.IsDirectoryChooser = true;
            lOpenDialogVM.SelectedFilePath = textBoxDestinationPath.Text;

            if (lOpenDialogVM.Show() == true)
            {
                textBoxDestinationPath.Text = lOpenDialogVM.SelectedFilePath;
            }
        }

        private void textBoxTaskName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.Name = textBoxTaskName.Text;
            }
        }

        private void textBox_LostFocus(object sender, RoutedEventArgs e)
        {
            FillComboBoxItems();
            SelectTaskByIndex(-1, false);
        }

        private void textBoxFileNameFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.FileFilterRegex = textBoxFileNameFilter.Text;
            }
        }

        private void textBoxBackupFileNamePattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.BackupFileNamePattern = textBoxBackupFileNamePattern.Text;
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // this code is for minimize window button "-" 
            if (fTray != null)
            {
                if (WindowState == WindowState.Minimized)
                    Hide();
                else if (WindowState == WindowState.Normal)
                    Show();
            }
        }

        private void buttonAbout_Click(object sender, RoutedEventArgs e)
        {
            if (fAbout != null)
                fAbout.Close();

            fAbout = new About();
            fAbout.Show();
        }

        private void buttonGetSavesPathDirectory_Click(object sender, RoutedEventArgs e)
        {
            Gat.Controls.OpenDialogView lOpenDialog = new Gat.Controls.OpenDialogView();
            Gat.Controls.OpenDialogViewModel lOpenDialogVM = (Gat.Controls.OpenDialogViewModel)lOpenDialog.DataContext;

            lOpenDialogVM.IsDirectoryChooser = true;
            lOpenDialogVM.SelectedFilePath = textBoxSavesPath.Text;

            if (lOpenDialogVM.Show() == true)
            {
                textBoxSavesPath.Text = lOpenDialogVM.SelectedFolder.Path;
                checkBoxAutomaticBackup.IsChecked = true;
            }
        }

        private void checkBoxAutomaticDestinationDirSizeLimit_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.DestinationDirSizeLimitEnabled = (checkBoxAutomaticDestinationDirSizeLimit.IsChecked == true);
            }
        }

        private void decimalUpDownDestinationDirSizeLimit_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (!fGUI_Reload)
            {
                BackupTask lBackupTask = fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex];

                lBackupTask.Settings.DestinationDirSizeLimit = (int)decimalUpDownDestinationDirSizeLimit.Value;
            }
        }

        private void ButtonTypicalLocations_Click(object sender, RoutedEventArgs e)
        {
            fTypicalLocationsMenu.PlacementTarget = this;
            fTypicalLocationsMenu.IsOpen = true;
        }

        private void ButtonRestore_Click(object sender, RoutedEventArgs e)
        {
            if (fRestore != null)
                fRestore.Close();

            fRestore = new Restore();
            if (fRestore.SetBackupMakerAndTask(fBackupMaker, fSettings.Settings.BackupTasks[fSelectedBackupTaskIndex]))
                fRestore.Show();
            else
                fRestore = null;
        }
    }
}
