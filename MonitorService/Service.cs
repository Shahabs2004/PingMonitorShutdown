using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.IO;

namespace PingMonitorService
{
    public partial class Service : ServiceBase
    {
        private CancellationTokenSource _cancellationTokenSource;
        private string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
        private string ipAddress = "10.7.1.30";
        private int shutdownTimeout = 60;
        private DateTime? firstFailureTime = null;
        private bool shutdownInitiated = false;

        public Service()
        {
            InitializeComponent();
            ServiceName = "PingMonitorService";
        }

        protected override void OnStart(string[] args)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => MonitorAsync(_cancellationTokenSource.Token));
        }

        protected override void OnStop()
        {
            _cancellationTokenSource?.Cancel();
            if (shutdownInitiated)
            {
                CancelShutdown();
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    string[] lines = File.ReadAllLines(settingsPath);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split('=');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            switch (key.ToLower())
                            {
                                case "ipaddress":
                                    ipAddress = value;
                                    break;
                                case "timeout":
                                    if (int.TryParse(value, out int timeout))
                                        shutdownTimeout = timeout;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Error loading settings: {ex.Message}", EventLogEntryType.Error);
            }
        }

        private async Task MonitorAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    LoadSettings(); // Reload settings on each iteration to pick up changes
                    bool pingSuccess = await PingAddressAsync(ipAddress);

                    if (!pingSuccess)
                    {
                        if (firstFailureTime == null)
                        {
                            firstFailureTime = DateTime.Now;
                            EventLog.WriteEntry(ServiceName, "Ping failed - starting countdown", EventLogEntryType.Warning);
                        }

                        TimeSpan timeSinceFailure = DateTime.Now - firstFailureTime.Value;
                        if (timeSinceFailure.TotalSeconds >= shutdownTimeout && !shutdownInitiated)
                        {
                            shutdownInitiated = true;
                            InitiateShutdown();
                        }
                    }
                    else
                    {
                        if (shutdownInitiated)
                        {
                            CancelShutdown();
                            EventLog.WriteEntry(ServiceName, "Connection restored - Shutdown cancelled", EventLogEntryType.Information);
                            shutdownInitiated = false;
                        }
                        firstFailureTime = null;
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry(ServiceName, $"Error in monitoring: {ex.Message}", EventLogEntryType.Error);
                }

                await Task.Delay(1000, cancellationToken); // Check every second
            }
        }

        private async Task<bool> PingAddressAsync(string address)
        {
            try
            {
                using (Ping ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(address, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private void InitiateShutdown()
        {
            try
            {
                EventLog.WriteEntry(ServiceName, "Initiating shutdown...", EventLogEntryType.Warning);
                ProcessStartInfo psi = new ProcessStartInfo("shutdown", $"/s /t {shutdownTimeout}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Failed to initiate shutdown: {ex.Message}", EventLogEntryType.Error);
                shutdownInitiated = false;
            }
        }

        private void CancelShutdown()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("shutdown", "/a")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Failed to cancel shutdown: {ex.Message}", EventLogEntryType.Error);
            }
        }
    }
}