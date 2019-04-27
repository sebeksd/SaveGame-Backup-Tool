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

using System.IO;

namespace SaveGameBackupTool
{
    public class PathHelper
    {
        private string fOriginalPath = "";
        private string fDirectoryPath = "";
        private string fFilePath = "";
        private string fFileName = "";

        private bool fIsFile = false;
        private bool fIsDirectory = false;
        private bool fIsEmpty = true;
        private bool fIsUnknown = true;

        public string DirectoryPath { get { return fDirectoryPath; } }
        public string FilePath { get { return fFilePath; } }
        public string FileName { get { return fFileName; } }

        public bool IsFile { get { return fIsFile; } }
        public bool IsDirectory { get { return fIsDirectory; } }
        public bool IsEmpty { get { return fIsEmpty; } }
        public bool IsNotEmpty { get { return !fIsEmpty; } }

        public bool PathCorrect { get { return (fIsFile || fIsDirectory); } }

        public PathHelper()
        {
        }

        private void Clear()
        {
            fOriginalPath = "";
            fDirectoryPath = "";
            fFilePath = "";
            fFileName = "";

            fIsFile = false;
            fIsDirectory = false;
            fIsEmpty = true;
        }

        public bool SetPath(string lPath)
        {
            Clear();

            fIsEmpty = string.IsNullOrEmpty(lPath);

            if (!fIsEmpty)
            { 
                fOriginalPath = lPath;
                fIsFile = File.Exists(lPath);

                if (fIsFile)
                {
                    fFilePath = lPath;
                    fFileName = Path.GetFileName(lPath);
                    fDirectoryPath = Path.GetDirectoryName(lPath);
                    fIsUnknown = false;
                }
                else if (Directory.Exists(lPath))
                {
                    fIsDirectory = true;
                    fDirectoryPath = lPath;
                    fIsUnknown = false;
                }
                else
                {
                    fIsUnknown = true;
                }
            }

            return PathCorrect;
        }

        public string[] GetAllFiles()
        {
            // Unknown path can occure when file/directory did not exist at the moment of SetPath, 
            // now we can make a check once more if path is correct
            if (fIsUnknown)
            {
                if (!SetPath(fOriginalPath))
                    return null;
            }

            if (fIsFile)
                return new string[] { fFilePath };
            else if (IsDirectory)
                return Directory.GetFiles(fDirectoryPath, "*", SearchOption.AllDirectories);
            else
                return null;
        }
    }
}
