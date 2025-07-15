using System;

namespace ZkemAPI.Core.Models
{
    /// <summary>
    /// Wyjątek rzucany gdy urządzenie jest zajęte
    /// </summary>
    public class DeviceBusyException : Exception
    {
        public DeviceStatusResponse Status { get; }

        public DeviceBusyException(DeviceStatusResponse status) 
            : base(status.Description)
        {
            Status = status;
        }
    }

    /// <summary>
    /// Wyjątek rzucany gdy urządzenie jest offline
    /// </summary>
    public class DeviceOfflineException : Exception
    {
        public DeviceStatusResponse Status { get; }

        public DeviceOfflineException(DeviceStatusResponse status) 
            : base(status.Description)
        {
            Status = status;
        }
    }
} 