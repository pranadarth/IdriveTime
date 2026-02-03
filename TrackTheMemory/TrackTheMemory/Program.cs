using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class MemoryLogger
{
    private static string _logFilePath; 
    private static double _totalMemoryRecorded = 0; 
    private static int _recordCount = 0; 
    private static bool _isApplicationRunning = true;

    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter the name of the application to track (without .exe):");
        string appName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(appName))
        {
            Console.WriteLine("Application name cannot be empty. Exiting.");
            return;
        }

        appName = appName.Trim();
        if (appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            appName = appName[..^4];
        }

        // Set the log file path in the Documents folder with the application name
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _logFilePath = Path.Combine(documentsPath, $"{appName}_MemoryUsageLog.txt");

        Console.WriteLine($"\nTracking memory usage for '{appName}'...");
        Console.WriteLine($"Log file will be saved in: {_logFilePath}");
        Console.WriteLine("Press Ctrl+C to stop and display the average memory usage.\n");
        Console.WriteLine("Live Data:");

        //Ctrl+C for clean shutdown
        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            eventArgs.Cancel = true; // Prevent immediate termination
            _isApplicationRunning = false; // Signal to stop the loop
        };

        await StartLoggingMemoryUsage(appName);

        double averageMemory = GetAverageMemoryUsage();
        string logEntry = $"\nAverage Memory Usage for '{appName}': {averageMemory:F2} MB";
        await LogToFile(logEntry);
        Console.WriteLine(logEntry);

    }

    private static async Task StartLoggingMemoryUsage(string appName)
    {
        while (_isApplicationRunning)
        {
            try
            {
                Process[] processes = Process.GetProcessesByName(appName);
                if (processes.Length == 0)
                {
                    Console.WriteLine($"No process found with the name '{appName}'. Exiting.");
                    _isApplicationRunning = false;
                    break;
                }

                // Assume the first matching process is the one to track
                Process targetProcess = processes[0];

                
                double memoryUsage = targetProcess.PrivateMemorySize64 / (1024.0 * 1024.0); // Convert bytes to MB
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                
                string logEntry = $"{timestamp} - {appName} Memory Usage: {memoryUsage:F2} MB";
                Console.WriteLine(logEntry);

                await LogToFile(logEntry);

                
                _totalMemoryRecorded += memoryUsage;
                _recordCount++;

                
                await Task.Delay(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking memory for '{appName}': {ex.Message}");
                _isApplicationRunning = false;
            }
        }
    }

    private static async Task LogToFile(string logEntry)
    {
        try
        {
            // Append the log entry to the file
            await File.AppendAllTextAsync(_logFilePath, logEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to write to log file: {ex.Message}");
        }
    }

    private static double GetAverageMemoryUsage()
    {
        return _recordCount > 0 ? _totalMemoryRecorded / _recordCount : 0;
    }
}
