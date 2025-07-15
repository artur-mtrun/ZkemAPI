using System;

namespace ZkemAPI.Core.Models
{
    /// <summary>
    /// Status czytnika
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// Czytnik jest dostępny i wolny
        /// </summary>
        Online = 0,
        
        /// <summary>
        /// Czytnik jest niedostępny (brak połączenia)
        /// </summary>
        Offline = 1,
        
        /// <summary>
        /// Czytnik jest dostępny ale zajęty (wykonuje operację)
        /// </summary>
        Busy = 2
    }

    /// <summary>
    /// Odpowiedź z informacją o statusie czytnika
    /// </summary>
    public class DeviceStatusResponse
    {
        /// <summary>
        /// Status czytnika
        /// </summary>
        public DeviceStatus Status { get; set; }
        
        /// <summary>
        /// Adres IP czytnika
        /// </summary>
        public string IpAddress { get; set; }
        
        /// <summary>
        /// Port czytnika
        /// </summary>
        public int Port { get; set; }
        
        /// <summary>
        /// Opis statusu
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Czas ostatniego sprawdzenia statusu
        /// </summary>
        public DateTime CheckedAt { get; set; }
        
        /// <summary>
        /// Czy urządzenie można użyć natychmiast
        /// </summary>
        public bool IsAvailable => Status == DeviceStatus.Online;
        
        /// <summary>
        /// Szacowany czas oczekiwania w sekundach (null jeśli nie dotyczy)
        /// </summary>
        public int? EstimatedWaitTimeSeconds { get; set; }

        public DeviceStatusResponse()
        {
            CheckedAt = DateTime.UtcNow;
        }

        public static DeviceStatusResponse CreateOnline(string ipAddress, int port)
        {
            return new DeviceStatusResponse
            {
                Status = DeviceStatus.Online,
                IpAddress = ipAddress,
                Port = port,
                Description = "Czytnik jest dostępny i gotowy do użycia",
                EstimatedWaitTimeSeconds = 0
            };
        }

        public static DeviceStatusResponse CreateOffline(string ipAddress, int port, string reason = null)
        {
            return new DeviceStatusResponse
            {
                Status = DeviceStatus.Offline,
                IpAddress = ipAddress,
                Port = port,
                Description = reason ?? "Czytnik jest niedostępny",
                EstimatedWaitTimeSeconds = null
            };
        }

        public static DeviceStatusResponse CreateBusy(string ipAddress, int port, int? estimatedWaitTime = null)
        {
            return new DeviceStatusResponse
            {
                Status = DeviceStatus.Busy,
                IpAddress = ipAddress,
                Port = port,
                Description = "Czytnik jest zajęty - wykonuje operację. Spróbuj ponownie za chwilę.",
                EstimatedWaitTimeSeconds = estimatedWaitTime
            };
        }
    }
} 