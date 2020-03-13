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

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.IO;
using System.Windows.Media;
using System.Threading;
using System.Windows.Threading;
using System;

namespace SaveGameBackupTool
{
    /// <summary>
    /// Logika interakcji dla klasy Restore.xaml
    /// </summary>
    public partial class Restore : Window
    {
        private BackupMaker fBackupMaker = null;
        private BackupTask fBackupTask = null;

        private static Action EmptyDelegate = delegate () { };

        public Restore()
        {
            InitializeComponent();
        }

        private bool ListFiles()
        {
            string lErrorMessage = "";
            string[] lFileList = fBackupMaker.GetBackupsList(fBackupTask, ref lErrorMessage);

            if (lFileList != null)
            {
                // convert file paths to FileNames
                for (int i = 0; i < lFileList.Length; i++)
                {
                    lFileList[i] = System.IO.Path.GetFileName(lFileList[i]);
                }

                ObservableCollection<string> lList = new ObservableCollection<string>(lFileList);
                listBoxBackupFiles.DataContext = lList;

                Binding lBinding = new Binding();
                listBoxBackupFiles.SetBinding(ListBox.ItemsSourceProperty, lBinding);
                listBoxBackupFiles.ScrollIntoView(listBoxBackupFiles.Items[listBoxBackupFiles.Items.Count - 1]);

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool SetBackupMakerAndTask(BackupMaker lBackupMaker, BackupTask lBackupTask)
        {
            fBackupMaker = lBackupMaker;
            fBackupTask = lBackupTask;

            this.Title = "Restore: " + lBackupTask.Settings.Name;

            return ListFiles();
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool BackupBeforeRestore()
        {
            string lErrorMessage = "";
            bool lResult = fBackupMaker.MakeBackup(fBackupTask, "-pre_restore", ref lErrorMessage);
            fBackupTask.SetLastBackupStatus(lResult, lErrorMessage);
            return lResult;
        }

        private void ButtonRestore_Click(object sender, RoutedEventArgs e)
        {
            labelStatus.Content = "Status: ...";
            labelStatus.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
            Thread.Sleep(1);

            if (checkBoxBackupBeforeRestore.IsChecked == true)
            {
                labelStatus.Content = "Status: backingup";
                labelStatus.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
                Thread.Sleep(1);
                if (!BackupBeforeRestore())
                {
                    labelStatus.Content = "Status: backup failed";
                    return;
                }
                else
                    ListFiles();
            }

            if ((listBoxBackupFiles.SelectedItem != null) && (fBackupMaker != null))
            {
                labelStatus.Content = "Status: restoring";
                labelStatus.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate);
                Thread.Sleep(1);

                if (fBackupMaker.RestoreBackup(fBackupTask, Path.GetFullPath(Path.Combine(fBackupTask.Settings.DestinationPathHelper.DirectoryPath, listBoxBackupFiles.SelectedItem.ToString()))))
                {
                    labelStatus.Content = "Status: restored";
                }
                else
                {
                    labelStatus.Content = "Status: error";
                }
            }
            else
            {
                labelStatus.Content = "Status: no item selected?";
            }
        }
        private void buttonRefresh_Click(object sender, RoutedEventArgs e)
        {
            ListFiles();
        }
    }
}
