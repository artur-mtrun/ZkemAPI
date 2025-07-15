namespace ZkemAPI.SDK.Models
{
    /// <summary>
    /// Ustawienia urządzenia i połączeń
    /// </summary>
    public class DeviceSettings
    {
        /// <summary>
        /// Maksymalny czas operacji na urządzeniu w minutach
        /// </summary>
        public int ConnectionTimeoutMinutes { get; set; } = 5;

        /// <summary>
        /// Timeout dla pojedynczych operacji komunikacyjnych w sekundach
        /// </summary>
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Liczba prób ponawiania operacji w przypadku błędu
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Opóźnienie między próbami w sekundach
        /// </summary>
        public int RetryDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Całkowity timeout w milisekundach
        /// </summary>
        public int TotalTimeoutMilliseconds => ConnectionTimeoutMinutes * 60 * 1000;

        /// <summary>
        /// Timeout operacji komunikacyjnych w milisekundach
        /// </summary>
        public int OperationTimeoutMilliseconds => ConnectionTimeoutSeconds * 1000;

        /// <summary>
        /// Opóźnienie retry w milisekundach
        /// </summary>
        public int RetryDelayMilliseconds => RetryDelaySeconds * 1000;
    }
} 