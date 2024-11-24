using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace PingMonitorService
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            serviceProcessInstaller = new ServiceProcessInstaller();
            serviceInstaller = new ServiceInstaller();

            // Service Account Information
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;

            // Service Information
            serviceInstaller.ServiceName = "PingMonitorService";
            serviceInstaller.DisplayName = "Ping Monitor Service";
            serviceInstaller.Description = "Monitors specified IP address and initiates shutdown on connection loss";
            serviceInstaller.StartType = ServiceStartMode.Automatic;

            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}