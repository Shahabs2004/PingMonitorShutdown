﻿using System.ServiceProcess;
using MonitorService;

namespace PingMonitorService
{
    static class Program
    {
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
                            {
                                new Service()
                            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}