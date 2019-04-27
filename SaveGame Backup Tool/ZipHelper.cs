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
using System.Text;

namespace SaveGameBackupTool
{
    // Based on http://stackoverflow.com/a/35416368
    public static class ZipHelper
    {
        private static string[] GetEntryNames(string[] names, string sourceFolder, bool includeBaseName)
        {
            if (names == null || names.Length == 0)
                return new string[0];

            if (includeBaseName)
                sourceFolder = Path.GetDirectoryName(sourceFolder);

            int length = string.IsNullOrEmpty(sourceFolder) ? 0 : sourceFolder.Length;
            if (length > 0 && sourceFolder != null && sourceFolder[length - 1] != Path.DirectorySeparatorChar && sourceFolder[length - 1] != Path.AltDirectorySeparatorChar)
                length++;

            var result = new string[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = names[i].Substring(length);
            }

            return result;
        }

        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName, CompressionLevel compressionLevel, bool includeBaseDirectory, Encoding entryNameEncoding, Predicate<string> filter)
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException("sourceDirectoryName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
                throw new ArgumentNullException("destinationArchiveFileName");
            }
            var filesToAdd = Directory.GetFiles(sourceDirectoryName, "*", SearchOption.AllDirectories);
            var entryNames = GetEntryNames(filesToAdd, sourceDirectoryName, includeBaseDirectory);
            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < filesToAdd.Length; i++)
                    {
                        // Add the following condition to do filtering:
                        if ((filter != null) && !filter(filesToAdd[i]))
                        {
                            continue;
                        }
                        archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                    }
                }
            }
        }

        public static void CreateFromFile(string sourceFilePath, string destinationArchiveFileName, CompressionLevel compressionLevel, bool includeBaseDirectory, Encoding entryNameEncoding, Predicate<string> filter)
        {
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                throw new ArgumentNullException("sourceFileName");
            }
            if (string.IsNullOrEmpty(destinationArchiveFileName))
            {
                throw new ArgumentNullException("destinationArchiveFileName");
            }

            string[] filesToAdd = { sourceFilePath };
            string[] entryNames = { Path.GetFileName(sourceFilePath) };

            using (var zipFileStream = new FileStream(destinationArchiveFileName, FileMode.Create))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < filesToAdd.Length; i++)
                    {
                        // Add the following condition to do filtering:
                        if ((filter != null) && !filter(filesToAdd[i]))
                        {
                            continue;
                        }
                        archive.CreateEntryFromFile(filesToAdd[i], entryNames[i], compressionLevel);
                    }
                }
            }
        }

        public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName)
        {
            if (string.IsNullOrEmpty(sourceArchiveFileName))
            {
                throw new ArgumentNullException("sourceArchiveFileName");
            }
            if (string.IsNullOrEmpty(destinationDirectoryName))
            {
                throw new ArgumentNullException("destinationDirectoryName");
            }

            // based on https://stackoverflow.com/a/14795752
            // this will not overwrite files: ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName);
            using (var zipFileStream = new FileStream(sourceArchiveFileName, FileMode.Open))
            {
                using (var archive = new ZipArchive(zipFileStream, ZipArchiveMode.Read))
                { 
                    DirectoryInfo lDirectory = Directory.CreateDirectory(destinationDirectoryName);
                    string destinationDirectoryFullPath = lDirectory.FullName;

                    foreach (ZipArchiveEntry file in archive.Entries)
                    {
                        string completeFileName = Path.GetFullPath(Path.Combine(destinationDirectoryFullPath, file.FullName));

                        if (!completeFileName.StartsWith(destinationDirectoryFullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new IOException("Trying to extract file outside of destination directory. See this link for more info: https://snyk.io/research/zip-slip-vulnerability");
                        }

                        // first recreate directory structure
                        Directory.CreateDirectory(Path.GetDirectoryName(completeFileName));
                        file.ExtractToFile(completeFileName, true);
                    }
                }
            }
        }
    }
}
