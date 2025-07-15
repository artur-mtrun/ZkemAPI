using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly IDeviceConnectionManager _deviceManager;

        public AttendanceController(IDeviceConnectionManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        /// <summary>
        /// Pobiera logi obecności z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia</param>
        /// <returns>Lista logów obecności</returns>
        [HttpPost("get-logs")]
        public async Task<IActionResult> GetAttendanceLogs([FromBody] AttendanceRequest request)
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
                        // Blokowanie urządzenia
                        device.EnableDevice(request.DeviceNumber, false);

                        var logs = new List<AttendanceLog>();

                        // Pobieranie danych
                        if (!device.ReadGeneralLogData(request.DeviceNumber))
                        {
                            throw new InvalidOperationException("Nie udało się odczytać logów z urządzenia");
                        }

                        // Pobieranie logów jeden po drugim
                        while (device.SSR_GetGeneralLogData(
                            request.DeviceNumber,
                            out string enrollNumber,
                            out int verifyMode,
                            out int inOutMode,
                            out int year,
                            out int month,
                            out int day,
                            out int hour,
                            out int minute,
                            out int second,
                            out int workCode))
                        {
                            logs.Add(new AttendanceLog
                            {
                                UserId = enrollNumber,
                                LogTime = new DateTime(year, month, day, hour, minute, second),
                                VerifyMode = (VerifyMode)verifyMode,
                                InOutMode = (InOutMode)inOutMode,
                                WorkCode = workCode
                            });
                        }

                        return logs.OrderByDescending(x => x.LogTime);
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
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas pobierania logów: {ex.Message}",
                    DeviceStatus = "Unknown"
                });
            }
        }

        /// <summary>
        /// Pobiera wszystkie logi obecności z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia</param>
        /// <returns>Lista wszystkich logów obecności</returns>
        [HttpPost("get-all-logs")]
        public async Task<IActionResult> GetAllAttendanceLogs([FromBody] AttendanceRequest request)
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
                        // Blokowanie urządzenia
                        device.EnableDevice(request.DeviceNumber, false);

                        var logs = new List<AttendanceLog>();

                        // Pobieranie logów jeden po drugim
                        while (device.SSR_GetGeneralLogData(
                            request.DeviceNumber,
                            out string enrollNumber,
                            out int verifyMode,
                            out int inOutMode,
                            out int year,
                            out int month,
                            out int day,
                            out int hour,
                            out int minute,
                            out int second,
                            out int workCode))
                        {
                            logs.Add(new AttendanceLog
                            {
                                UserId = enrollNumber,
                                LogTime = new DateTime(year, month, day, hour, minute, second),
                                VerifyMode = (VerifyMode)verifyMode,
                                InOutMode = (InOutMode)inOutMode,
                                WorkCode = workCode
                            });
                        }

                        return logs.OrderByDescending(x => x.LogTime);
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
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas pobierania logów: {ex.Message}",
                    DeviceStatus = "Unknown"
                });
            }
        }
    }

    public class AttendanceRequest
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
    }
} 