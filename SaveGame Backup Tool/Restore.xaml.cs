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

namespace SaveGameBackupTool
{
    /// <summary>
    /// Logika interakcji dla klasy Restore.xaml
    /// </summary>
    public partial class Restore : Window
    {
        private BackupMaker fBackupMaker = null;
        private BackupTask fBackupTask = null;

        public Restore()
        {
            InitializeComponent();
        }

        public bool SetBackupMakerAndTask(BackupMaker lBackupMaker, BackupTask lBackupTask)
        {
            fBackupMaker = lBackupMaker;
            fBackupTask = lBackupTask;

            string lErrorMessage = "";
            string[] lFileList = fBackupMaker.GetBackupsList(lBackupTask, ref lErrorMessage);
           
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

                return true;
            }
            else
            {
                return false;
            }
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ButtonRestore_Click(object sender, RoutedEventArgs e)
        {
            if ((listBoxBackupFiles.SelectedItem != null) && (fBackupMaker != null))             
                fBackupMaker.RestoreBackup(fBackupTask, Path.GetFullPath(Path.Combine(fBackupTask.Settings.DestinationPathHelper.DirectoryPath, listBoxBackupFiles.SelectedItem.ToString())));
        }
    }
}
