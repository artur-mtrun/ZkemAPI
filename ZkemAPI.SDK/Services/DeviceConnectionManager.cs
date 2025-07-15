using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.SDK.Models;

namespace ZkemAPI.SDK.Services
{
    /// <summary>
    /// Manager połączeń zarządzający dostępem do czytników.
    /// Zapewnia że do każdego czytnika może być tylko jedno aktywne połączenie w danym momencie.
    /// </summary>
    public class DeviceConnectionManager : IDeviceConnectionManager, IDisposable
    {
        private readonly ILogger<DeviceConnectionManager> _logger;
        private readonly Func<IZkemDevice> _deviceFactory;
        private readonly ConcurrentDictionary<string, DeviceSemaphore> _deviceSemaphores;
        private readonly object _lockObject = new object();
        private readonly DeviceSettings _deviceSettings;
        private bool _disposed = false;

        public DeviceConnectionManager(ILogger<DeviceConnectionManager> logger, Func<IZkemDevice> deviceFactory, IOptions<DeviceSettings> deviceSettings)
        {
            _logger = logger;
            _deviceFactory = deviceFactory;
            _deviceSemaphores = new ConcurrentDictionary<string, DeviceSemaphore>();
            _deviceSettings = deviceSettings.Value;
        }

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu
        /// </summary>
        public async Task<T> ExecuteDeviceOperationAsync<T>(string ipAddress, int port, Func<IZkemDevice, T> operation)
        {
            using var cts = new CancellationTokenSource(_deviceSettings.TotalTimeoutMilliseconds);
            return await ExecuteDeviceOperationAsync(ipAddress, port, operation, cts.Token);
        }

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu i timeout'em
        /// </summary>
        public async Task<T> ExecuteDeviceOperationAsync<T>(string ipAddress, int port, Func<IZkemDevice, T> operation, CancellationToken cancellationToken)
        {
            var deviceKey = GetDeviceKey(ipAddress, port);
            var deviceSemaphore = GetOrCreateDeviceSemaphore(deviceKey);

            _logger.LogDebug("Oczekiwanie na dostęp do czytnika {DeviceKey}. Oczekujące operacje: {PendingCount}", 
                deviceKey, deviceSemaphore.Semaphore.CurrentCount);

            await deviceSemaphore.Semaphore.WaitAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Rozpoczęcie operacji na czytniku {DeviceKey} z timeout'em {TimeoutMinutes} minut", 
                    deviceKey, _deviceSettings.ConnectionTimeoutMinutes);
                
                var device = _deviceFactory();
                
                // Wykonanie operacji z retry logic
                return await ExecuteWithRetryAsync(operation, device, deviceKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Operacja na czytniku {DeviceKey} została anulowana z powodu timeout'a", deviceKey);
                throw new TimeoutException($"Operacja na czytniku {deviceKey} przekroczyła limit czasu {_deviceSettings.ConnectionTimeoutMinutes} minut");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas operacji na czytniku {DeviceKey}", deviceKey);
                throw;
            }
            finally
            {
                deviceSemaphore.Semaphore.Release();
                _logger.LogDebug("Zakończenie operacji na czytniku {DeviceKey}", deviceKey);
            }
        }

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu (bez zwrócenia wartości)
        /// </summary>
        public async Task ExecuteDeviceOperationAsync(string ipAddress, int port, Action<IZkemDevice> operation)
        {
            using var cts = new CancellationTokenSource(_deviceSettings.TotalTimeoutMilliseconds);
            await ExecuteDeviceOperationAsync(ipAddress, port, operation, cts.Token);
        }

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu i timeout'em (bez zwrócenia wartości)
        /// </summary>
        public async Task ExecuteDeviceOperationAsync(string ipAddress, int port, Action<IZkemDevice> operation, CancellationToken cancellationToken)
        {
            await ExecuteDeviceOperationAsync<object>(ipAddress, port, device =>
            {
                operation(device);
                return null;
            }, cancellationToken);
        }

        /// <summary>
        /// Sprawdza ile operacji czeka w kolejce dla danego czytnika
        /// </summary>
        public int GetPendingOperationsCount(string ipAddress, int port)
        {
            var deviceKey = GetDeviceKey(ipAddress, port);
            
            if (_deviceSemaphores.TryGetValue(deviceKey, out var deviceSemaphore))
            {
                // CurrentCount = 0 oznacza że semaphore jest zajęty
                // CurrentCount = 1 oznacza że semaphore jest wolny
                return deviceSemaphore.Semaphore.CurrentCount == 0 ? 1 : 0;
            }

            return 0;
        }

        /// <summary>
        /// Zwalnia zasoby dla określonego czytnika
        /// </summary>
        public void ReleaseDevice(string ipAddress, int port)
        {
            var deviceKey = GetDeviceKey(ipAddress, port);
            
            if (_deviceSemaphores.TryRemove(deviceKey, out var deviceSemaphore))
            {
                deviceSemaphore.Dispose();
                _logger.LogDebug("Zwolniono zasoby dla czytnika {DeviceKey}", deviceKey);
            }
        }

        /// <summary>
        /// Wymusza zwolnienie zawieszonych operacji na czytniku
        /// </summary>
        public void ForceReleaseDevice(string ipAddress, int port)
        {
            var deviceKey = GetDeviceKey(ipAddress, port);
            
            if (_deviceSemaphores.TryGetValue(deviceKey, out var deviceSemaphore))
            {
                try
                {
                    // Próba zwolnienia semaphore jeśli jest zablokowany
                    if (deviceSemaphore.Semaphore.CurrentCount == 0)
                    {
                        deviceSemaphore.Semaphore.Release();
                        _logger.LogWarning("Wymuszono zwolnienie zawieszonych operacji na czytniku {DeviceKey}", deviceKey);
                    }
                }
                catch (SemaphoreFullException)
                {
                    // Semaphore już jest wolny
                    _logger.LogDebug("Semaphore dla czytnika {DeviceKey} już jest wolny", deviceKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas wymuszania zwolnienia semaphore dla czytnika {DeviceKey}", deviceKey);
                }
            }
        }

        /// <summary>
        /// Wyczyść wszystkie zawieszenie operacje
        /// </summary>
        public void CleanupHangingOperations()
        {
            _logger.LogInformation("Rozpoczęcie czyszczenia zawieszonych operacji");
            
            foreach (var kvp in _deviceSemaphores)
            {
                var deviceKey = kvp.Key;
                var deviceSemaphore = kvp.Value;
                
                try
                {
                    if (deviceSemaphore.Semaphore.CurrentCount == 0)
                    {
                        deviceSemaphore.Semaphore.Release();
                        _logger.LogWarning("Wyczyszczono zawieszoną operację na czytniku {DeviceKey}", deviceKey);
                    }
                }
                catch (SemaphoreFullException)
                {
                    // Semaphore już jest wolny
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas czyszczenia operacji na czytniku {DeviceKey}", deviceKey);
                }
            }
            
            _logger.LogInformation("Zakończenie czyszczenia zawieszonych operacji");
        }

        /// <summary>
        /// Wykonuje operację z retry logic
        /// </summary>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<IZkemDevice, T> operation, IZkemDevice device, string deviceKey, CancellationToken cancellationToken)
        {
            var attempt = 0;
            Exception lastException = null;

            while (attempt < _deviceSettings.RetryAttempts)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logger.LogDebug("Próba {Attempt}/{MaxAttempts} wykonania operacji na czytniku {DeviceKey}", 
                        attempt + 1, _deviceSettings.RetryAttempts, deviceKey);
                    
                    return operation(device);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    lastException = ex;
                    attempt++;
                    
                    _logger.LogWarning("Próba {Attempt}/{MaxAttempts} operacji na czytniku {DeviceKey} nieudana: {Error}", 
                        attempt, _deviceSettings.RetryAttempts, deviceKey, ex.Message);
                    
                    if (attempt < _deviceSettings.RetryAttempts)
                    {
                        await Task.Delay(_deviceSettings.RetryDelayMilliseconds, cancellationToken);
                    }
                }
            }

            _logger.LogError("Wszystkie {MaxAttempts} próby operacji na czytniku {DeviceKey} nieudane", 
                _deviceSettings.RetryAttempts, deviceKey);
            
            throw lastException ?? new InvalidOperationException($"Operacja na czytniku {deviceKey} nieudana po {_deviceSettings.RetryAttempts} próbach");
        }

        /// <summary>
        /// Tworzy klucz identyfikujący czytnik
        /// </summary>
        private static string GetDeviceKey(string ipAddress, int port)
        {
            return $"{ipAddress}:{port}";
        }

        /// <summary>
        /// Pobiera lub tworzy semaphore dla czytnika
        /// </summary>
        private DeviceSemaphore GetOrCreateDeviceSemaphore(string deviceKey)
        {
            return _deviceSemaphores.GetOrAdd(deviceKey, key =>
            {
                _logger.LogDebug("Tworzenie nowego semaphore dla czytnika {DeviceKey}", key);
                return new DeviceSemaphore();
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_lockObject)
                {
                    if (!_disposed)
                    {
                        _logger.LogInformation("Zwalnianie wszystkich zasobów DeviceConnectionManager");
                        
                        foreach (var kvp in _deviceSemaphores)
                        {
                            kvp.Value.Dispose();
                        }
                        
                        _deviceSemaphores.Clear();
                        _disposed = true;
                    }
                }
            }
        }

        /// <summary>
        /// Klasa pomocnicza przechowująca semaphore dla czytnika
        /// </summary>
        private class DeviceSemaphore : IDisposable
        {
            public SemaphoreSlim Semaphore { get; }

            public DeviceSemaphore()
            {
                // Każdy czytnik może mieć tylko jedno aktywne połączenie
                Semaphore = new SemaphoreSlim(1, 1);
            }

            public void Dispose()
            {
                Semaphore?.Dispose();
            }
        }
    }
} 