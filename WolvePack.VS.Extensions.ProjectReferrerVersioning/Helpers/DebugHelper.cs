using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace WolvePack.VS.Extensions.ProjectReferrerVersioning.Helpers
{
    /// <summary>
    /// Static helper class for debug messages using file logging in Visual Studio extensions
    /// </summary>
    public static class DebugHelper
    {
        private static bool _debugEnabled = true;
        private static readonly string _logFilePath = @"C:\Temp\WolvePack.PRV.txt";
        private static readonly object _lockObject = new object();

        public static bool DebugEnabled
        {
            get => _debugEnabled;
            set => _debugEnabled = value;
        }

        static DebugHelper()
        {
            InitializeLog();
        }

        /// <summary>
        /// Initialize the log file by clearing it
        /// </summary>
        public static void InitializeLog()
        {
            if (_debugEnabled)
            {
                try
                {
                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(_logFilePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Clear log file and write header
                    lock (_lockObject)
                    {
                        File.WriteAllText(_logFilePath, $"=== WolvePack PRV Debug Log ===\n");
                        File.AppendAllText(_logFilePath, $"Session: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Debug log init failed: {ex.Message}", "Debug Log Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        /// <summary>
        /// Write to debug log file
        /// </summary>
        private static void WriteToLog(string level, string message, string title = "")
        {
            if (!_debugEnabled) return;

            try
            {
                lock (_lockObject)
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    string logEntry = $"[{timestamp}] [{level}] [T{threadId}] {(!string.IsNullOrEmpty(title) ? $"[{title}] " : "")}{message}\n";
                    
                    File.AppendAllText(_logFilePath, logEntry);
                }
            }
            catch
            {
                // Ignore file write errors to prevent infinite loops
            }
        }

        public static void ShowDebug(string message, string title = "Debug")
        {
            WriteToLog("DEBUG", message, title);
        }

        public static void ShowError(string message, string title = "Error")
        {
            WriteToLog("ERROR", message, title);
            MessageBox.Show(message, $"[ERROR] {title}", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowWarning(string message, string title = "Warning")
        {
            WriteToLog("WARN", message, title);
            MessageBox.Show(message, $"[WARNING] {title}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowInfo(string message, string title = "Info")
        {
            WriteToLog("INFO", message, title);
            MessageBox.Show(message, $"[INFO] {title}", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static void Log(string message, string context = "General")
        {
            WriteToLog("LOG", message, context);
        }

        public static void LogSeparator(string sectionName = "")
        {
            if (_debugEnabled)
            {
                string separator = string.IsNullOrEmpty(sectionName) 
                    ? "\n" + new string('=', 80) + "\n" 
                    : $"\n{'=',30} {sectionName} {'=',30}\n";
                WriteToLog("SEP", separator);
            }
        }

        /// <summary>
        /// Open the log file in default text editor
        /// </summary>
        public static void OpenLogFile()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    System.Diagnostics.Process.Start(_logFilePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log: {ex.Message}", "Log Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}