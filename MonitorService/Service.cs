/*
using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace MonitorService
{
    public partial class Service : ServiceBase
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly ServiceConfiguration _config;
        private readonly INetworkMonitor _networkMonitor;
        private readonly IShutdownManager _shutdownManager;
        private DateTime? _firstFailureTime;

        public Service()
        {
            InitializeComponent();
            ServiceName = "PingMonitorService";
            _cancellationTokenSource = new CancellationTokenSource();
            _config = new ServiceConfiguration(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"));
            _networkMonitor = new PingMonitor();
            _shutdownManager = new WindowsShutdownManager();
        }

        protected override void OnStart(string[] args)
        {
            Task.Run(() => MonitorAsync(_cancellationTokenSource.Token));
        }

        protected override void OnStop()
        {
            _cancellationTokenSource?.Cancel();
            _shutdownManager.CancelShutdown();
        }

        private async Task MonitorAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _config.LoadSettings();
                    await ProcessNetworkStatusAsync();
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    LogError("Error in monitoring", ex);
                }
            }
        }

        private async Task ProcessNetworkStatusAsync()
        {
            bool isConnected = await _networkMonitor.CheckConnectionAsync(_config.IpAddress);

            if (!isConnected)
            {
                HandleConnectionFailure();
            }
            else
            {
                HandleConnectionRestored();
            }
        }

        private void HandleConnectionFailure()
        {
            _firstFailureTime ??= DateTime.Now;

            TimeSpan timeSinceFailure = DateTime.Now - _firstFailureTime.Value;
            if (timeSinceFailure.TotalSeconds >= _config.ShutdownTimeout && !_shutdownManager.IsShutdownInitiated)
            {
                LogWarning("Initiating shutdown due to connection failure");
                _shutdownManager.InitiateShutdown(_config.ShutdownTimeout);
            }
        }

        private void HandleConnectionRestored()
        {
            if (_shutdownManager.IsShutdownInitiated)
            {
                _shutdownManager.CancelShutdown();
                LogInfo("Connection restored - Shutdown cancelled");
            }
            _firstFailureTime = null;
        }

        private void LogError(string message, Exception ex) =>
            EventLog.WriteEntry(ServiceName, $"{message}: {ex.Message}", EventLogEntryType.Error);

        private void LogWarning(string message) =>
            EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Warning);

        private void LogInfo(string message) =>
            EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Information);
    }

    public interface INetworkMonitor
    {
        Task<bool> CheckConnectionAsync(string address);
    }

    public class PingMonitor : INetworkMonitor
    {
        public async Task<bool> CheckConnectionAsync(string address)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, 1000);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }
    }

    public interface IShutdownManager
    {
        bool IsShutdownInitiated { get; }
        void InitiateShutdown(int timeout);
        void CancelShutdown();
    }

    public class WindowsShutdownManager : IShutdownManager
    {
        public bool IsShutdownInitiated { get; private set; }

        public void InitiateShutdown(int timeout)
        {
            try
            {
                Process.Start(new ProcessStartInfo("shutdown", $"/s /t {timeout}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                IsShutdownInitiated = true;
            }
            catch
            {
                IsShutdownInitiated = false;
                throw;
            }
        }

        public void CancelShutdown()
        {
            if (!IsShutdownInitiated) return;

            Process.Start(new ProcessStartInfo("shutdown", "/a")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            IsShutdownInitiated = false;
        }
    }

    public class ServiceConfiguration
    {
        private readonly string _configPath;
        public string IpAddress { get; private set; } = "10.7.1.30";
        public int ShutdownTimeout { get; private set; } = 60;

        public ServiceConfiguration(string configPath)
        {
            _configPath = configPath;
        }

        public void LoadSettings()
        {
            if (!File.Exists(_configPath)) return;

            foreach (var line in File.ReadAllLines(_configPath))
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                var key = parts[0].Trim().ToLower();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "ipaddress":
                        IpAddress = value;
                        break;
                    case "timeout":
                        if (int.TryParse(value, out int timeout))
                            ShutdownTimeout = timeout;
                        break;
                }
            }
        }
    }



}
*/

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