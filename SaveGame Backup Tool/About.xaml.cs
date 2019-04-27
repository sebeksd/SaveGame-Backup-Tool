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

using System.Reflection;
using System.Windows;

namespace SaveGameBackupTool
{
    /// <summary>
    /// Logika interakcji dla klasy About.xaml
    /// </summary>
    public partial class About : Window
    {
        public About()
        {
            InitializeComponent();

            //labelAppName.Content = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
            labelVersionNumber.Content = Assembly.GetEntryAssembly().GetName().Version;

            //AssemblyInfo entryAssemblyInfo = new AssemblyInfo(Assembly.GetEntryAssembly());
            AssemblyCopyrightAttribute lCopyright = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0] as AssemblyCopyrightAttribute;

            textBoxInfo.Text =
                lCopyright.Copyright + "\r\n" +
                "This application is licensed under LGPL 3.0\r\n\r\n" +
                "License should be provided with application or can be found at:\r\n" +
                "https://www.gnu.org/licenses/ \r\n\r\n" +
                "Source files can be found at:\r\n" +
                "https://github.com/sebeksd/SaveGame-Backup-Tool \r\n\r\n" +
                "Application exe can be found at:\r\n" +
                "https://github.com/sebeksd/SaveGame-Backup-Tool/releases \r\n\r\n" +
                "If you like this app and you think it is worth of your money or you just have to much money, feel free to donate to my Bitcoin Address:\r\n" +
                "1B3TSZx38u1gRT6B7UN156oiUJNkKvGb56"
                ;
        }

        private void buttonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
