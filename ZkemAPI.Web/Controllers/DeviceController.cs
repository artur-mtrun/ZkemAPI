using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;
using System;
using System.Collections.Generic;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly IZkemDevice _zkemDevice;

        public DeviceController(IZkemDevice zkemDevice)
        {
            _zkemDevice = zkemDevice;
        }

        [HttpGet("connect")]
        public IActionResult TestConnection(string ip, int port = 4370)
        {
            try
            {
                bool connected = _zkemDevice.Connect_Net(ip, port);
                
                if (connected)
                {
                    // Test połączenia poprzez pobranie czasu z urządzenia
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    bool timeOk = _zkemDevice.GetDeviceTime(1, ref year, ref month, ref day, ref hour, ref minute, ref second);

                    var deviceTime = timeOk 
                        ? $"Czas urządzenia: {year}-{month}-{day} {hour}:{minute}:{second}"
                        : "Nie udało się pobrać czasu urządzenia";

                    return Ok(new { 
                        status = "Połączono", 
                        deviceTime 
                    });
                }

                return BadRequest("Nie udało się połączyć z urządzeniem");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd: {ex.Message}");
            }
        }

        [HttpGet("disconnect")]
        public IActionResult Disconnect()
        {
            try
            {
                _zkemDevice.Disconnect();
                return Ok("Rozłączono");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Błąd podczas rozłączania: {ex.Message}");
            }
        }

        /// <summary>
        /// Ustawia datę i czas w czytniku
        /// </summary>
        /// <param name="request">Parametry połączenia i nowa data</param>
        /// <returns>Status operacji</returns>
        [HttpPost("set-time")]
        public IActionResult SetDeviceTime([FromBody] SetTimeRequest request)
        {
            try
            {
                // 1. Połączenie z czytnikiem
                if (!_zkemDevice.Connect_Net(request.IpAddress, request.Port))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Nie udało się połączyć z czytnikiem"
                    });
                }

                try
                {
                    // 2. Blokowanie urządzenia
                    _zkemDevice.EnableDevice(request.DeviceNumber, false);

                    // 3. Ustawianie czasu
                    DateTime newTime = request.DateTime ?? DateTime.Now;
                    bool success = _zkemDevice.SetDeviceTime2(request.DeviceNumber, 
                        newTime.Year, newTime.Month, newTime.Day, 
                        newTime.Hour, newTime.Minute, newTime.Second);

                    if (!success)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Nie udało się ustawić czasu w czytniku"
                        });
                    }

                    // 4. Pobieranie aktualnego czasu dla weryfikacji
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    bool timeOk = _zkemDevice.GetDeviceTime(request.DeviceNumber, 
                        ref year, ref month, ref day, ref hour, ref minute, ref second);

                    var currentTime = timeOk
                        ? new DateTime(year, month, day, hour, minute, second)
                        : (DateTime?)null;

                    return Ok(new
                    {
                        Success = true,
                        Message = "Czas został ustawiony pomyślnie",
                        SetTime = newTime,
                        CurrentDeviceTime = currentTime
                    });
                }
                finally
                {
                    // 5. Odblokowujemy urządzenie przed rozłączeniem
                    _zkemDevice.EnableDevice(request.DeviceNumber, true);
                    // 6. Rozłączamy się z czytnikiem
                    _zkemDevice.Disconnect();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas ustawiania czasu: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Pobiera informacje o czytniku
        /// </summary>
        [HttpPost("get-info")]
        public IActionResult GetDeviceInfo([FromBody] DeviceRequest request)
        {
            try
            {
                if (!_zkemDevice.Connect_Net(request.IpAddress, request.Port))
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Nie udało się połączyć z czytnikiem"
                    });
                }

                try
                {
                    _zkemDevice.EnableDevice(request.DeviceNumber, false);

                    // Podstawowe informacje
                    string firmwareVer = string.Empty;
                    string mac = string.Empty;
                    string platform = string.Empty;
                    string serialNumber = string.Empty;
                    string manufacturerCode = string.Empty;
                    int deviceType = 0;

                    _zkemDevice.GetFirmwareVersion(request.DeviceNumber, ref firmwareVer);
                    _zkemDevice.GetDeviceMAC(request.DeviceNumber, ref mac);
                    _zkemDevice.GetPlatform(request.DeviceNumber, ref platform);
                    _zkemDevice.GetSerialNumber(request.DeviceNumber, ref serialNumber);
                    _zkemDevice.GetDeviceStrInfo(request.DeviceNumber, 1, out manufacturerCode);
                    _zkemDevice.GetDeviceInfo(request.DeviceNumber, 1, ref deviceType);

                    // Czas urządzenia
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;
                    _zkemDevice.GetDeviceTime(
                        request.DeviceNumber, 
                        ref year, ref month, ref day, 
                        ref hour, ref minute, ref second);

                    // Statystyki
                    int userCount = 0;
                    int managerCount = 0;
                    int fingerCount = 0;
                    int recordCount = 0;
                    int pwdCount = 0;
                    int oplogCount = 0;
                    int faceCount = 0;

                    // Pobieramy listę wszystkich użytkowników i liczymy ich ręcznie
                    bool success = _zkemDevice.ReadAllUserID(request.DeviceNumber);
                    if (success)
                    {
                        string enrollNumber = string.Empty;
                        while (_zkemDevice.SSR_GetAllUserInfo(
                            request.DeviceNumber,
                            out enrollNumber,
                            out string name,
                            out string password,
                            out int privilege,
                            out bool enabled))
                        {
                            if (privilege > 0)
                            {
                                managerCount++;
                            }
                            else
                            {
                                userCount++;
                            }
                        }
                    }

                    // Pozostałe statystyki
                    _zkemDevice.GetDeviceStatus(request.DeviceNumber, 3, ref fingerCount);    // Liczba odcisków palców
                    _zkemDevice.GetDeviceStatus(request.DeviceNumber, 4, ref pwdCount);       // Liczba haseł
                    _zkemDevice.GetDeviceStatus(request.DeviceNumber, 5, ref recordCount);    // Liczba logów
                    _zkemDevice.GetDeviceStatus(request.DeviceNumber, 6, ref oplogCount);     // Liczba logów operacji
                    _zkemDevice.GetDeviceStatus(request.DeviceNumber, 21, ref faceCount);     // Liczba twarzy

                    return Ok(new
                    {
                        Success = true,
                        Data = new
                        {
                            // Informacje podstawowe
                            FirmwareVersion = firmwareVer,
                            MacAddress = mac,
                            Platform = platform,
                            SerialNumber = serialNumber,
                            ManufacturerCode = manufacturerCode,
                            DeviceType = deviceType,

                            // Czas urządzenia
                            DeviceTime = new DateTime(year, month, day, hour, minute, second),

                            // Statystyki
                            Statistics = new
                            {
                                UserCount = userCount,
                                ManagerCount = managerCount,
                                FingerCount = fingerCount,
                                PasswordCount = pwdCount,
                                RecordCount = recordCount,
                                OperationLogCount = oplogCount,
                                FaceCount = faceCount
                            }
                        }
                    });
                }
                finally
                {
                    _zkemDevice.EnableDevice(request.DeviceNumber, true);
                    _zkemDevice.Disconnect();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas pobierania informacji o czytniku: {ex.Message}"
                });
            }
        }
    }

    public class SetTimeRequest
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        public DateTime? DateTime { get; set; }  // Jeśli null, zostanie użyty aktualny czas
    }
} 