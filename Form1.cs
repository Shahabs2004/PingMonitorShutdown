/*using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace PingMonitorShutdown
{
    public partial class Form1 : Form
    {
        private string ipAddress = "10.7.1.30"; // Default IP
        private bool isMonitoring = false;
        private DateTime? firstFailureTime = null;
        private System.Windows.Forms.Timer checkTimer;
        private bool shutdownInitiated = false;
        private int shutdownTimeout = 60; // Default timeout in seconds
        private string settingsPath = "settings.ini";

        private TextBox txtIpAddress;
        private TextBox txtTimeout;
        private Button btnStartStop;
        private Label lblStatus;
        private Label lblCountdown;

        public Form1()
        {
            LoadSettings();
            InitializeComponents();
            InitializeTimer();
            StartMonitoring(); // Auto-start monitoring
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
                MessageBox.Show($"Error loading settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                string[] settings = new string[]
                {
                    $"IPAddress={ipAddress}",
                    $"Timeout={shutdownTimeout}"
                };
                File.WriteAllLines(settingsPath, settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}");
            }
        }

        private void InitializeComponents()
        {
            this.Size = new System.Drawing.Size(400, 250);
            this.Text = "Ping Monitor";

            // IP Address input
            var lblIpAddress = new Label
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(80, 20),
                Text = "IP Address:"
            };

            txtIpAddress = new TextBox
            {
                Location = new System.Drawing.Point(100, 20),
                Size = new System.Drawing.Size(150, 20),
                Text = ipAddress
            };
            txtIpAddress.TextChanged += TxtIpAddress_TextChanged;

            // Timeout input
            var lblTimeout = new Label
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(80, 20),
                Text = "Timeout (s):"
            };

            txtTimeout = new TextBox
            {
                Location = new System.Drawing.Point(100, 50),
                Size = new System.Drawing.Size(150, 20),
                Text = shutdownTimeout.ToString()
            };
            txtTimeout.TextChanged += TxtTimeout_TextChanged;

            btnStartStop = new Button
            {
                Location = new System.Drawing.Point(270, 20),
                Size = new System.Drawing.Size(100, 25),
                Text = "Stop Monitoring"  // Changed default text since we auto-start
            };
            btnStartStop.Click += BtnStartStop_Click;

            lblStatus = new Label
            {
                Location = new System.Drawing.Point(20, 90),
                Size = new System.Drawing.Size(350, 20),
                Text = "Status: Not monitoring"
            };

            lblCountdown = new Label
            {
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(350, 20),
                Text = ""
            };

            this.Controls.AddRange(new Control[] {
                lblIpAddress, txtIpAddress,
                lblTimeout, txtTimeout,
                btnStartStop, lblStatus, lblCountdown
            });
        }

        private void TxtIpAddress_TextChanged(object sender, EventArgs e)
        {
            ipAddress = txtIpAddress.Text;
            SaveSettings();
        }

        private void TxtTimeout_TextChanged(object sender, EventArgs e)
        {
            if (int.TryParse(txtTimeout.Text, out int timeout))
            {
                shutdownTimeout = timeout;
                SaveSettings();
            }
        }

        private void StartMonitoring()
        {
            isMonitoring = true;
            btnStartStop.Text = "Stop Monitoring";
            firstFailureTime = null;
            shutdownInitiated = false;
            checkTimer.Start();
            lblStatus.Text = "Status: Monitoring started";
        }

        private void InitializeTimer()
        {
            checkTimer = new System.Windows.Forms.Timer();
            checkTimer.Interval = 1000; // Check every 1 second
            checkTimer.Tick += CheckTimer_Tick;
        }

        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isMonitoring)
            {
                if (string.IsNullOrWhiteSpace(ipAddress))
                {
                    MessageBox.Show("Please enter a valid IP address.");
                    return;
                }
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
                if (shutdownInitiated)
                {
                    CancelShutdown();
                }
            }
        }

        private void StopMonitoring()
        {
            isMonitoring = false;
            checkTimer.Stop();
            btnStartStop.Text = "Start Monitoring";
            lblStatus.Text = "Status: Not monitoring";
            lblCountdown.Text = "";
            firstFailureTime = null;
            shutdownInitiated = false;
        }

        private async void CheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                bool pingSuccess = await PingAddressAsync(ipAddress);

                if (!pingSuccess)
                {
                    if (firstFailureTime == null)
                    {
                        firstFailureTime = DateTime.Now;
                    }

                    TimeSpan timeSinceFailure = DateTime.Now - firstFailureTime.Value;
                    int remainingSeconds = shutdownTimeout - (int)timeSinceFailure.TotalSeconds;

                    if (remainingSeconds <= 0 && !shutdownInitiated)
                    {
                        lblStatus.Text = "Status: Initiating shutdown...";
                        shutdownInitiated = true;
                        InitiateShutdown();
                    }
                    else if (!shutdownInitiated)
                    {
                        lblStatus.Text = "Status: Ping failed";
                        lblCountdown.Text = $"Shutdown in: {remainingSeconds} seconds";
                    }
                }
                else
                {
                    if (shutdownInitiated)
                    {
                        CancelShutdown();
                        lblStatus.Text = "Status: Connection restored - Shutdown cancelled";
                        shutdownInitiated = false;
                    }
                    else
                    {
                        lblStatus.Text = "Status: Ping successful";
                    }
                    firstFailureTime = null;
                    lblCountdown.Text = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
                StopMonitoring();
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
                ProcessStartInfo psi = new ProcessStartInfo("shutdown", $"/s /t {shutdownTimeout}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initiate shutdown: {ex.Message}");
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
                MessageBox.Show($"Failed to cancel shutdown: {ex.Message}");
            }
        }
    }
}*/
using System;
using System.Windows.Forms;
using System.IO;
using System.ServiceProcess;

namespace PingMonitorConfig
{
    public partial class Form1 : Form
    {
        private string settingsPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "settings.ini");
        private TextBox txtIpAddress;
        private TextBox txtTimeout;
        private Label lblStatus;
        private Button btnSave;

        public Form1()
        {
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Size = new System.Drawing.Size(400, 200);
            this.Text = "Ping Monitor Configuration";

            var lblIpAddress = new Label
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(80, 20),
                Text = "IP Address:"
            };

            txtIpAddress = new TextBox
            {
                Location = new System.Drawing.Point(100, 20),
                Size = new System.Drawing.Size(150, 20)
            };

            var lblTimeout = new Label
            {
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(80, 20),
                Text = "Timeout (s):"
            };

            txtTimeout = new TextBox
            {
                Location = new System.Drawing.Point(100, 50),
                Size = new System.Drawing.Size(150, 20)
            };

            btnSave = new Button
            {
                Location = new System.Drawing.Point(100, 80),
                Size = new System.Drawing.Size(100, 25),
                Text = "Save Settings"
            };
            btnSave.Click += BtnSave_Click;

            lblStatus = new Label
            {
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(350, 20),
                Text = "Service Status: Checking..."
            };

            this.Controls.AddRange(new Control[] {
                lblIpAddress, txtIpAddress,
                lblTimeout, txtTimeout,
                btnSave, lblStatus
            });

            CheckServiceStatus();
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
                                    txtIpAddress.Text = value;
                                    break;
                                case "timeout":
                                    txtTimeout.Text = value;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading settings: {ex.Message}");
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                string[] settings = new string[]
                {
                    $"IPAddress={txtIpAddress.Text}",
                    $"Timeout={txtTimeout.Text}"
                };
                File.WriteAllLines(settingsPath, settings);
                MessageBox.Show("Settings saved successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}");
            }
        }

        private void CheckServiceStatus()
        {
            try
            {
                ServiceController sc = new ServiceController("PingMonitorService");
                lblStatus.Text = $"Service Status: {sc.Status}";
            }
            catch
            {
                lblStatus.Text = "Service Status: Not Installed";
            }
        }
    }
}