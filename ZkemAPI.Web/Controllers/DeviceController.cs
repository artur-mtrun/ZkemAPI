using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;
using ZkemAPI.SDK.Services;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly IDeviceConnectionManager _deviceManager;

        public DeviceController(IDeviceConnectionManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        [HttpGet("connect")]
        public async Task<IActionResult> TestConnection(string ip, int port = 4370)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(ip, port, device =>
                {
                    // Próba połączenia
                    if (!device.Connect_Net(ip, port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Test podstawowego połączenia - sprawdzamy wersję firmware
                        string version = "";
                        bool deviceSuccess = device.GetFirmwareVersion(1, ref version);
                        
                        if (!deviceSuccess)
                        {
                            throw new InvalidOperationException("Połączenie nawiązane, ale nie można pobrać informacji z urządzenia");
                        }

                        return new { Version = version };
                    }
                    finally
                    {
                        device.Disconnect();
                    }
                });

                return Ok(new
                {
                    Success = true,
                    Message = $"Połączenie z czytnikiem {ip}:{port} nawiązane pomyślnie",
                    Data = result,
                    DeviceStatus = "Online"
                });
            }
            catch (DeviceBusyException ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Status.Description,
                    DeviceStatus = ex.Status.Status.ToString(),
                    EstimatedWaitTime = ex.Status.EstimatedWaitTimeSeconds
                });
            }
            catch (DeviceOfflineException ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = ex.Status.Description,
                    DeviceStatus = ex.Status.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Błąd połączenia z czytnikiem {ip}:{port}: {ex.Message}",
                    DeviceStatus = "Unknown"
                });
            }
        }

        [HttpGet("disconnect")]
        public IActionResult Disconnect()
        {
            try
            {
                return Ok(new
                {
                    Success = true,
                    Message = "Rozłączenie wykonywane automatycznie po każdej operacji"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Success = false, Message = ex.Message });
            }
        }

        [HttpPost("set-time")]
        public async Task<IActionResult> SetDeviceTime([FromBody] SetTimeRequest request)
        {
            try
            {
                var deviceTime = request.DateTime ?? DateTime.Now;
                
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                }

                try
                {
                        // Blokowanie urządzenia podczas operacji
                        device.EnableDevice(request.DeviceNumber, false);

                        // Ustawianie czasu urządzenia
                        bool success = device.SetDeviceTime2(request.DeviceNumber, 
                            deviceTime.Year, deviceTime.Month, deviceTime.Day,
                            deviceTime.Hour, deviceTime.Minute, deviceTime.Second);

                    if (!success)
                    {
                            throw new InvalidOperationException("Nie udało się ustawić czasu urządzenia");
                    }

                        // Pobieramy aktualny czas dla potwierdzenia
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                        device.GetDeviceTime(request.DeviceNumber, ref year, ref month, ref day, ref hour, ref minute, ref second);

                        return new { 
                            RequestedTime = deviceTime,
                            ActualTime = new DateTime(year, month, day, hour, minute, second)
                        };
                    }
                    finally
                    {
                        // Odblokowujemy urządzenie przed rozłączeniem
                        device.EnableDevice(request.DeviceNumber, true);
                        device.Disconnect();
                    }
                });

                    return Ok(new
                    {
                        Success = true,
                    Message = "Czas urządzenia został ustawiony pomyślnie",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Błąd podczas ustawiania czasu: {ex.Message}"
                });
            }
        }

        [HttpPost("get-info")]
        public async Task<IActionResult> GetDeviceInfo([FromBody] DeviceRequest request)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                }

                try
                {
                        var deviceInfo = new
                        {
                            FirmwareVersion = GetDeviceStringInfo(device, request.DeviceNumber, d => {
                                string version = "";
                                d.GetFirmwareVersion(request.DeviceNumber, ref version);
                                return version;
                            }),
                            SerialNumber = GetDeviceStringInfo(device, request.DeviceNumber, d => {
                                string serial = "";
                                d.GetSerialNumber(request.DeviceNumber, ref serial);
                                return serial;
                            }),
                            MAC = GetDeviceStringInfo(device, request.DeviceNumber, d => {
                                string mac = "";
                                d.GetDeviceMAC(request.DeviceNumber, ref mac);
                                return mac;
                            }),
                            Platform = GetDeviceStringInfo(device, request.DeviceNumber, d => {
                                string platform = "";
                                d.GetPlatform(request.DeviceNumber, ref platform);
                                return platform;
                            }),
                            DeviceTime = GetDeviceTime(device, request.DeviceNumber),
                            UserCount = GetDeviceIntInfo(device, request.DeviceNumber, 2), // FCR_USER
                            RecordCount = GetDeviceIntInfo(device, request.DeviceNumber, 8), // FCR_RECORDCOUNT
                            AdminCount = GetDeviceIntInfo(device, request.DeviceNumber, 4), // FCR_ADMINSTATUS
                            MaxUsers = GetDeviceIntInfo(device, request.DeviceNumber, 1) // FCR_CAPACITY
                        };

                        return deviceInfo;
                    }
                    finally
                    {
                        device.Disconnect();
                    }
                });

                    return Ok(new
                    {
                        Success = true,
                    Message = "Informacje o urządzeniu pobrane pomyślnie",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                        {
                    Success = false,
                    Message = $"Błąd podczas pobierania informacji o urządzeniu: {ex.Message}"
                });
            }
        }

        private static string GetDeviceStringInfo(IZkemDevice device, int deviceNumber, Func<IZkemDevice, string> getter)
        {
            try
            {
                return getter(device) ?? "Niedostępne";
            }
            catch
            {
                return "Błąd odczytu";
            }
        }

        private static int GetDeviceIntInfo(IZkemDevice device, int deviceNumber, int infoType)
        {
            try
            {
                int value = 0;
                device.GetDeviceInfo(deviceNumber, infoType, ref value);
                return value;
            }
            catch
            {
                return -1;
            }
        }

        private static string GetDeviceTime(IZkemDevice device, int deviceNumber)
        {
            try
            {
                int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                bool success = device.GetDeviceTime(deviceNumber, ref year, ref month, ref day, ref hour, ref minute, ref second);
                
                if (success)
                {
                    return new DateTime(year, month, day, hour, minute, second).ToString("yyyy-MM-dd HH:mm:ss");
                }
                return "Niedostępny";
            }
            catch
            {
                return "Błąd odczytu";
            }
        }

        [HttpPost("check-connection")]
        public async Task<IActionResult> CheckConnection([FromBody] DeviceRequest request)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Próba połączenia z timeout
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Test komunikacji - próba pobrania numeru seryjnego
                        string serialNumber = "";
                        bool success = device.GetSerialNumber(request.DeviceNumber, ref serialNumber);
                        
                        return new { 
                            Connected = true,
                            SerialNumber = success ? serialNumber : "Nieznany",
                            ResponseTime = DateTime.Now
                        };
                }
                finally
                {
                        device.Disconnect();
                }
                });

                return Ok(new
                {
                    Success = true,
                    Message = "Połączenie z czytnikiem działa poprawnie",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Brak połączenia z czytnikiem: {ex.Message}",
                    Data = new { Connected = false }
                });
            }
        }
  
        [HttpPost("clear-logs")]
        public async Task<IActionResult> ClearAllLogs([FromBody] DeviceRequest request)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Blokowanie urządzenia podczas operacji
                        device.EnableDevice(request.DeviceNumber, false);

                        // Pobieramy liczę rekordów przed czyszczeniem
                        int recordsBefore = GetDeviceIntInfo(device, request.DeviceNumber, 8);

                        // Czyszczenie wszystkich logów
                        bool success = device.ClearGLog(request.DeviceNumber);
                        
                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się wyczyścić logów");
                        }

                        // Pobieramy liczbę rekordów po czyszczeniu
                        int recordsAfter = GetDeviceIntInfo(device, request.DeviceNumber, 8);

                        return new { 
                            RecordsBefore = recordsBefore,
                            RecordsAfter = recordsAfter,
                            ClearedRecords = recordsBefore - recordsAfter
                        };
                    }
                    finally
                    {
                        // Odblokowujemy urządzenie przed rozłączeniem
                        device.EnableDevice(request.DeviceNumber, true);
                        device.Disconnect();
                    }
                });

                        return Ok(new
                        {
                            Success = true,
                    Message = $"Logi zostały wyczyszczone. Usunięto {result.ClearedRecords} rekordów.",
                    Data = result
                        });
                    }
            catch (Exception ex)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                    Message = $"Błąd podczas czyszczenia logów: {ex.Message}"
                });
            }
        }

        [HttpPost("restart")]
        public async Task<IActionResult> RestartDevice([FromBody] DeviceRequest request)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Restart urządzenia
                        bool success = device.RestartDevice(request.DeviceNumber);
                        
                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się zrestartować urządzenia");
                    }

                        return new { RestartTime = DateTime.Now };
                }
                finally
                {
                        // Nie wywołujemy Disconnect() ponieważ urządzenie jest restartowane
                        // i połączenie zostanie automatycznie przerwane
                    }
                });

                return Ok(new
                {
                    Success = true,
                    Message = "Urządzenie zostało pomyślnie zrestartowane",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Błąd podczas restartu urządzenia: {ex.Message}"
                });
            }
        }

        [HttpPost("beep")]
        public async Task<IActionResult> PlayBeep([FromBody] BeepRequest request)
        {
            try
            {
                // Walidacja czasu trwania
                if (request.Duration < 100 || request.Duration > 3000)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Czas trwania sygnału musi być między 100 a 3000 milisekund"
                    });
                }

                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                }

                try
                {
                        // Blokowanie urządzenia
                        device.EnableDevice(request.DeviceNumber, false);

                        // Odtwarzanie sygnału dźwiękowego
                        bool success = device.Beep(request.Duration);
                    
                    if (!success)
                    {
                            throw new InvalidOperationException("Nie udało się odtworzyć sygnału dźwiękowego");
                        }

                        return new { Duration = request.Duration };
                    }
                    finally
                    {
                        // Odblokowujemy urządzenie przed rozłączeniem
                        device.EnableDevice(request.DeviceNumber, true);
                        // Rozłączamy się z czytnikiem
                        device.Disconnect();
                    }
                });

                    return Ok(new
                    {
                        Success = true,
                    Message = $"Sygnał dźwiękowy odtworzony przez {result.Duration}ms"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Błąd podczas odtwarzania sygnału: {ex.Message}"
                });
            }
        }

        [HttpPost("play-voice")]
        public async Task<IActionResult> PlayVoice([FromBody] VoiceRequest request)
        {
            try
            {
                // Walidacja parametrów
                if (request.VoiceIndex < 0 || request.VoiceIndex > 9)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Indeks komunikatu głosowego musi być między 0 a 9"
                    });
                }

                if (request.Length < 100 || request.Length > 5000)
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Długość komunikatu musi być między 100 a 5000 milisekund"
                    });
                }

                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Blokowanie urządzenia
                        device.EnableDevice(request.DeviceNumber, false);

                        // Odtwarzanie komunikatu głosowego
                        bool success = device.PlayVoice(request.VoiceIndex, request.Length);
                        
                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się odtworzyć komunikatu głosowego");
                        }

                        return new { VoiceIndex = request.VoiceIndex, Length = request.Length };
                }
                finally
                {
                        // Odblokowujemy urządzenie przed rozłączeniem
                        device.EnableDevice(request.DeviceNumber, true);
                        // Rozłączamy się z czytnikiem
                        device.Disconnect();
                }
                });

                return Ok(new
                {
                    Success = true,
                    Message = $"Komunikat głosowy {result.VoiceIndex} odtworzony przez {result.Length}ms"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Błąd podczas odtwarzania komunikatu głosowego: {ex.Message}"
                });
            }
        }
    
    }

    public class SetTimeRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        public DateTime? DateTime { get; set; }
    }

        public class DeviceRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
    }

    public class BeepRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        /// <summary>
        /// Czas trwania sygnału w milisekundach (100-3000ms)
        /// </summary>
        public int Duration { get; set; } = 500;
    }

    public class VoiceRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        /// <summary>
        /// Indeks komunikatu głosowego (0-9 dla standardowych komunikatów)
        /// </summary>
        public int VoiceIndex { get; set; } = 0;
        /// <summary>
        /// Długość odtwarzania komunikatu w milisekundach (100-5000ms)
        /// </summary>
        public int Length { get; set; } = 1000;
    }
} 