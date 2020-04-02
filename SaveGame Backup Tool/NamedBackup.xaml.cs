using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SaveGameBackupTool
{
    /// <summary>
    /// Logika interakcji dla klasy NamedBackup.xaml
    /// </summary>
    public partial class NamedBackup : Window
    {
        public NamedBackup()
        {
            InitializeComponent();
            textBoxName.Focus();
            textBoxName.SelectAll();
        }

        public string GetBackupName()
        {
            return textBoxName.Text;
        }

        private void buttonCreate_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        private void textBoxName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                buttonCreate_Click(sender, e);
            }
        }
    }
}
