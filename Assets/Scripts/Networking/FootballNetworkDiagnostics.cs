using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

public static class FootballNetworkDiagnostics
{
    private const string DirectoryName = "NetworkDiagnostics";
    private static readonly object FileLock = new object();
    private static readonly string SessionStamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
    private static readonly int ProcessId = GetProcessId();
    private static string _logPath;
    private static bool _headerWritten;

    public static string DirectoryPath => Path.Combine(Application.persistentDataPath, DirectoryName);

    public static string LogPath
    {
        get
        {
            if (string.IsNullOrEmpty(_logPath))
                _logPath = Path.Combine(DirectoryPath, $"network-{SessionStamp}-pid{ProcessId}.log");

            return _logPath;
        }
    }

    public static void Write(string scope, string message)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(DirectoryPath);

                if (!_headerWritten)
                {
                    File.AppendAllText(LogPath, BuildHeader(), Encoding.UTF8);
                    _headerWritten = true;
                }

                string line = $"{DateTime.UtcNow:O} [frame={Time.frameCount}] [{scope}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning($"Could not write network diagnostics: {exception.Message}");
        }
    }

    private static string BuildHeader()
    {
        return
            $"Football network diagnostic session{Environment.NewLine}" +
            $"UTC: {DateTime.UtcNow:O}{Environment.NewLine}" +
            $"PID: {ProcessId}{Environment.NewLine}" +
            $"Unity: {Application.unityVersion}{Environment.NewLine}" +
            $"Platform: {Application.platform}{Environment.NewLine}" +
            $"Editor: {Application.isEditor}; BatchMode: {Application.isBatchMode}{Environment.NewLine}" +
            $"Arguments: {string.Join(" ", Environment.GetCommandLineArgs())}{Environment.NewLine}" +
            $"PersistentDataPath: {Application.persistentDataPath}{Environment.NewLine}" +
            new string('-', 100) + Environment.NewLine;
    }

    private static int GetProcessId()
    {
        try
        {
            return Process.GetCurrentProcess().Id;
        }
        catch
        {
            return 0;
        }
    }
}
