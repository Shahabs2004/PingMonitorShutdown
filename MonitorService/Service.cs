using System;
using System.ServiceProcess;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;

namespace MonitorService
{
    public partial class MonitorService : ServiceBase
    {
        private readonly string _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServiceLog.txt");
        private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt");
        private CancellationTokenSource _cancellationTokenSource;
        private string _ipAddress = "10.7.1.1"; // Default to Google DNS
        private int _timeout = 30000; // Default 30 seconds
        private bool _shutdownInitiated = false;

        public MonitorService()
        {
            InitializeComponent();
            this.ServiceName = "PingMonitorService";
            this.CanStop = true;
            this.CanPauseAndContinue = false;
            this.AutoLog = false;
        }

        protected override void OnStart(string[] args)
        {
            LoadSettings();
            _cancellationTokenSource = new CancellationTokenSource();
            LogInfo("Service started");
            Task.Run(() => MonitorConnection(_cancellationTokenSource.Token));
        }

        protected override void OnStop()
        {
            _cancellationTokenSource?.Cancel();
            LogInfo("Service stopped");
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    string[] settings = File.ReadAllLines(_settingsPath);
                    foreach (string setting in settings)
                    {
                        if (setting.StartsWith("IPAddress="))
                            _ipAddress = setting.Substring(10);
                        else if (setting.StartsWith("Timeout="))
                            int.TryParse(setting.Substring(8), out _timeout);
                    }
                }
                LogInfo($"Settings loaded - IP: {_ipAddress}, Timeout: {_timeout}ms");
            }
            catch (Exception ex)
            {
                LogError("Error loading settings", ex);
            }
        }

        private async Task MonitorConnection(CancellationToken cancellationToken)
        {
            using (Ping ping = new Ping())
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        PingReply reply = await ping.SendPingAsync(_ipAddress);
                        if (reply.Status == IPStatus.Success)
                        {
                            if (_shutdownInitiated)
                            {
                                _shutdownInitiated = false;
                                LogInfo("Connection restored - Shutdown cancelled");
                            }
                        }
                        else
                        {
                            LogWarning($"Ping failed: {reply.Status}");
                            if (!_shutdownInitiated)
                            {
                                _shutdownInitiated = true;
                                LogWarning("Connection lost - Initiating shutdown");
                                InitiateShutdown();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("Error in ping monitoring", ex);
                    }

                    await Task.Delay(_timeout, cancellationToken);
                }
            }
        }

        private void InitiateShutdown()
        {
            try
            {
                Process.Start("shutdown", "/s /t 60 /c \"Network connection lost - System will shutdown in 60 seconds\"");
                LogInfo("Shutdown command initiated");
            }
            catch (Exception ex)
            {
                LogError("Failed to initiate shutdown", ex);
            }
        }

        private void LogToFile(string message, string logLevel)
        {
            try
            {
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {message}";
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Failed to write to log file: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private void LogError(string message, Exception ex)
        {
            string fullMessage = $"{message}: {ex.Message}";
            EventLog.WriteEntry(ServiceName, fullMessage, EventLogEntryType.Error);
            LogToFile(fullMessage, "ERROR");
        }

        private void LogWarning(string message)
        {
            EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Warning);
            LogToFile(message, "WARNING");
        }

        private void LogInfo(string message)
        {
            EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Information);
            LogToFile(message, "INFO");
        }

        private void InitializeComponent()
        {
            // Windows service component initialization
            this.ServiceName = "PingMonitorService";
        }
    }
}