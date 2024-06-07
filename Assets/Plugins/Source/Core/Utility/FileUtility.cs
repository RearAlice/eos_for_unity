/*
 * Copyright (c) 2024 PlayEveryWare
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace PlayEveryWare.EpicOnlineServices.Utility
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;

    /// <summary>
    /// Utility class used for a variety of File tasks.
    /// </summary>
    public static class FileUtility
    {
        /// <summary>
        /// Interval with which to update progress, in milliseconds
        /// </summary>
        private const int DefaultUpdateIntervalMS = 1500;

        #region Data Structures 

        /// <summary>
        /// Stores information regarding a file copy operation.
        /// </summary>
        public struct CopyFileOperation
        {
            /// <summary>
            /// The fully-qualified path of the source file.
            /// </summary>
            public string From;

            /// <summary>
            /// The fully-qualified path to copy the file to. The path does not
            /// have to exist, and in fact might not.
            /// </summary>
            public string To;

            /// <summary>
            /// The number of bytes in the file to copy.
            /// </summary>
            public long Bytes;
        }
        /// <summary>
        /// Contains information about the progress of a copy file operation.
        /// </summary>
        public struct CopyFileProgressInfo
        {
            /// <summary>
            /// The number of files that have been copied.
            /// </summary>
            public int FilesCopied;

            /// <summary>
            /// The total number of files being copied.
            /// </summary>
            public int TotalFilesToCopy;

            /// <summary>
            /// The size in bytes of the files that have been copied.
            /// </summary>
            public long BytesCopied;

            /// <summary>
            /// The total size in bytes of all the files being copied.
            /// </summary>
            public long TotalBytesToCopy;
        }

        #endregion

        /// <summary>
        /// Generates a unique and new temporary directory inside the Temporary Cache Path as determined by Unity,
        /// and returns the fully-qualified path to the newly created directory.
        /// </summary>
        /// <returns>Fully-qualified file path to the newly generated directory.</returns>
        public static string GenerateTempDirectory()
        {
            // Generate a temporary directory path.
            string tempDirectory = Path.Combine(Application.temporaryCachePath, $"/Output-{Guid.NewGuid()}/");

            // If (by some crazy miracle) the directory path already exists, keep generating until there is a new one.
            while (Directory.Exists(tempDirectory))
            {
                tempDirectory = Path.Combine(Application.temporaryCachePath, $"/Output-{Guid.NewGuid()}/");
            }

            // Create the directory.
            Directory.CreateDirectory(tempDirectory);

            // return the fully-qualified path to the newly created directory.
            return Path.GetFullPath(tempDirectory);
        }

        /// <summary>
        /// Get a list of the directories that are represented by the filepaths
        /// provided. The list is unique, and is ordered by smallest path first,
        /// so that the list can be utilized to create each directory in-order
        /// if that is useful
        /// </summary>
        /// <param name="filepaths">
        /// The filepaths to get the directories for.
        /// </param>
        /// <param name="inOrder">
        /// If true, return the list of directories by order shortest path
        /// first.
        /// </param>
        /// <returns></returns>
        private static IEnumerable<string> GetDirectories(
            IEnumerable<string> filepaths, bool inOrder = true)
        {
            // For each filepath, determine the immediate parent directory of
            // the file. Make a unique set of these by utilizing a HashSet.
            ISet<string> directoriesToCreate = new HashSet<string>();
            foreach (var path in filepaths)
            {
                string parent = Path.GetDirectoryName(path);

                if (null != parent)
                    directoriesToCreate.Add(parent);
                
            }

            // Return the list of directories to create in ascending order of
            // string length.
            return inOrder ? directoriesToCreate.OrderBy(s => s.Length) : 
                directoriesToCreate;
        }

        /// <summary>
        /// Deconstructs a filepath to return the path of each directory in the
        /// path.
        /// </summary>
        /// <param name="filePath">
        /// The filepath to get the directories of.
        /// </param>
        /// <returns>A list of directories for the path.</returns>
        private static List<string> GetDirectories(string filePath)
        {
            // Get the directory name from the file path.
            string directoryPath = Path.GetDirectoryName(filePath);

            // To store the directories along the path.
            List<string> directories = new() { filePath };

            // If the directory path is empty or null, then stop.
            if (string.IsNullOrEmpty(directoryPath))
            {
                return directories;
            }

            // Split the directory path into individual directories.
            string[] parts = directoryPath.Split(
                Path.DirectorySeparatorChar, 
                Path.AltDirectorySeparatorChar, 
                StringSplitOptions.RemoveEmptyEntries
                );

            // If there are no parts to process, then stop
            if (parts.Length == 0)
                return directories;

            // Reconstruct each directory step-by-step to maintain full path.
            string currentPath = parts[0];
            foreach (string part in parts[1..])
            {
                currentPath = Path.Combine(currentPath, part);
                directories.Add(currentPath);
            }

            return directories;
        }

        /// <summary>
        /// Run a list of file copy operations. If the destination directory
        /// indicated does not exist, it will be created. It is expected that
        /// the source file indicated exists. Default behavior is to overwrite
        /// destination files.
        ///
        /// Before the file copy operations begin, the copy file operations are
        /// inspected for missing destination directories - and the directory
        /// structure required is created in its entirety before file copy
        /// operations commence.
        /// 
        /// If a progress interface is provided, the file copy operations will
        /// be randomized so as to average out the number of bytes copied at
        /// each interval. Otherwise the files will be copied in the order they
        /// are in the provided operations parameter.
        /// </summary>
        /// <param name="operations">File copy operations to perform.</param>
        /// <param name="updateIntervalMS">
        /// The interval in milliseconds with which to report progress.
        /// </param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task</returns>
        public static async Task ExecuteCopyFileOperationsAsync(
            List<CopyFileOperation> operations,
            IProgress<CopyFileProgressInfo> progress = null,
            CancellationToken cancellationToken = default,
            Int32 updateIntervalMS = DefaultUpdateIntervalMS)
        {
            // Create struct to track and report on progress
            CopyFileProgressInfo progressInfo = new()
            {
                TotalBytesToCopy = operations.Sum(o => o.Bytes),
                TotalFilesToCopy = operations.Count()
            };

            if (null != progress)
            {
                // Create timer to periodically report on the progress based on
                // the interval (at some future point it may be appropriate to
                // make that interval a parameter)
                await using Timer progressTimer = new(state =>
                {
                    progress?.Report(progressInfo);
                }, null, 0, updateIntervalMS);
            }

            // Get directories represented by the set of file copy operations
            var directoriesToCreate = GetDirectories(
                operations.Select(o => o.To)
                );

            // Create each directory
            foreach (var directory in directoriesToCreate)
            {
                Directory.CreateDirectory(directory);
            }
            
            // Execute each file copy operation.
            foreach (var copyOperation in operations)
            {
                // Make sure to throw an exception if a cancellation was
                // requested of the token.
                cancellationToken.ThrowIfCancellationRequested();

                // Run the file copy asynchronously, passing on the cancellation
                // token.
                await Task.Run(() => File.Copy(
                        copyOperation.From,
                        copyOperation.To, true),
                    cancellationToken);

                // If there is no progress, we don't need to update the info
                // and we can continue.
                if (null == progress)
                    continue;

                // Increment the number of files copied.
                progressInfo.FilesCopied++;

                // Update the number of bytes that have been copied.
                progressInfo.BytesCopied += copyOperation.Bytes;
            }
        }

        /// <summary>
        /// Returns the root of the Unity project.
        /// </summary>
        /// <returns>Fully-qualified file path to the root of the Unity project.</returns>
        public static string GetProjectPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        }

#if UNITY_EDITOR
        #region Line Ending Manipulations

        public static void ConvertDosToUnixLineEndings(string filename)
        {
            ConvertDosToUnixLineEndings(filename, filename);
        }

        public static void ConvertDosToUnixLineEndings(string srcFilename, string destFilename)
        {
            const byte CR = 0x0d;

            var fileAsBytes = File.ReadAllBytes(srcFilename);

            using (var filestream = File.OpenWrite(destFilename))
            {
                var writer = new BinaryWriter(filestream);
                int filePosition = 0;
                int indexOfDOSNewline = 0;

                do
                {
                    indexOfDOSNewline = Array.IndexOf<byte>(fileAsBytes, CR, filePosition);

                    if (indexOfDOSNewline >= 0)
                    {
                        writer.Write(fileAsBytes, filePosition, indexOfDOSNewline - filePosition);
                        filePosition = indexOfDOSNewline + 1;
                    }
                    else if (filePosition < fileAsBytes.Length)
                    {
                        writer.Write(fileAsBytes, filePosition, fileAsBytes.Length - filePosition);
                    }

                } while (indexOfDOSNewline > 0);

                // truncate trailing garbage.
                filestream.SetLength(filestream.Position);
            }
        }

        #endregion
#endif
        /// <summary>
        /// Reads all text from the indicated file.
        /// </summary>
        /// <param name="path">Filepath to the file to read from.</param>
        /// <returns>The contents of the file at the indicated path as a string.</returns>
        public static string ReadAllText(string path)
        {
            string text = string.Empty;
#if UNITY_ANDROID && !UNITY_EDITOR
            text = AndroidFileIOHelper.ReadAllText(path);
#else
            text = File.ReadAllText(path);
#endif
            return text;
        }

        /// <summary>
        /// Asynchronously reads all text from the indicated file.
        /// </summary>
        /// <param name="path">The file to read from.</param>
        /// <returns>Task</returns>
        public static async Task<string> ReadAllTextAsync(string path)
        {
            
            return await File.ReadAllTextAsync(path);
        }

        public static void NormalizePath(ref string path)
        {
            char toReplace = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';
            path = path.Replace(toReplace, Path.DirectorySeparatorChar);
        }

#if UNITY_EDITOR

        public static void CleanDirectory(string directoryPath, bool ignoreGit = true)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogWarning($"Cannot clean directory \"{directoryPath}\", because it does not exist.");
                return;
            }

            try
            {
                foreach (string subDir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories))
                {
                    // Skip .git directories 
                    if (ignoreGit && subDir.EndsWith(".git")) { continue; }
                    
                    // TODO: This is a little bit dangerous as one developer has found out. If the output directory is not
                    //       empty, and contains directories and files unrelated to output, this will (without prompting)
                    //       delete them. So, if you're outputting to, say the "Desktop" directory, it will delete everything
                    //       on your desktop (zoinks!)
                    if (Directory.Exists(subDir))
                        Directory.Delete(subDir, true);
                }

                foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName is ".gitignore" or ".gitattributes" && Path.GetDirectoryName(file) == directoryPath)
                    {
                        if (Path.GetDirectoryName(file) == directoryPath)
                        {
                            continue; // Skip these files if they are in the root directory
                        }
                    }

                    if (File.Exists(file))
                        File.Delete(file);
                }

                Debug.Log($"Finished cleaning directory \"{directoryPath}\".");
            }
            catch (Exception ex)
            {
                Debug.Log($"An error (which was ignored) occurred while cleaning \"{directoryPath}\": {ex.Message}");
            }
        }


        public static void OpenDirectory(string path)
        {
            // Correctly format the path based on the operating system.
            // For Windows, the path format is fine as is.
            // For macOS, use the "open" command.
            // For Linux, use the "xdg-open" command.
            path = path.Replace("/", "\\"); // Replace slashes for Windows compatibility

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                System.Diagnostics.Process.Start("open", path);
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                System.Diagnostics.Process.Start("xdg-open", path);
            }
        }
#endif
    }
}
