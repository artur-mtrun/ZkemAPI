using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AttendanceController : ControllerBase
    {
        private readonly IZkemDevice _zkemDevice;

        public AttendanceController(IZkemDevice zkemDevice)
        {
            _zkemDevice = zkemDevice;
        }

        /// <summary>
        /// Pobiera logi obecności z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia</param>
        /// <returns>Lista logów obecności</returns>
        [HttpPost("get-logs")]
        public IActionResult GetAttendanceLogs([FromBody] AttendanceRequest request)
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

                    var logs = new List<AttendanceLog>();

                    // 3. Pobieranie danych
                    if (!_zkemDevice.ReadGeneralLogData(request.DeviceNumber))
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Nie udało się odczytać logów z urządzenia"
                        });
                    }

                    // 4. Pobieranie logów jeden po drugim
                    while (_zkemDevice.SSR_GetGeneralLogData(
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

                    return Ok(new
                    {
                        Success = true,
                        Data = logs.OrderByDescending(x => x.LogTime)
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
                    Message = $"Błąd podczas pobierania logów: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Pobiera wszystkie logi obecności z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia</param>
        /// <returns>Lista wszystkich logów obecności</returns>
        [HttpPost("get-all-logs")]
        public IActionResult GetAllAttendanceLogs([FromBody] AttendanceRequest request)
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

                    var logs = new List<AttendanceLog>();

                    // 3. Pobieranie logów jeden po drugim
                    while (_zkemDevice.SSR_GetGeneralLogData(
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

                    return Ok(new
                    {
                        Success = true,
                        Data = logs.OrderByDescending(x => x.LogTime)
                    });
                }
                finally
                {
                    // 4. Odblokowujemy urządzenie przed rozłączeniem
                    _zkemDevice.EnableDevice(request.DeviceNumber, true);
                    // 5. Rozłączamy się z czytnikiem
                    _zkemDevice.Disconnect();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas pobierania logów: {ex.Message}"
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