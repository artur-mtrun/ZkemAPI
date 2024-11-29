using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;
using System;
using System.Collections.Generic;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly IZkemDevice _zkemDevice;

        public LogController(IZkemDevice zkemDevice)
        {
            _zkemDevice = zkemDevice;
        }

        /// <summary>
        /// Pobiera logi operacji administracyjnych
        /// </summary>
        [HttpPost("operations")]
        public IActionResult GetOperationLogs([FromBody] LogRequest request)
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

                    var logs = new List<OperationLog>();

                    int index = 0;
                    int adminId = 0;
                    int operation = 0;
                    int param1 = 0, param2 = 0, param3 = 0;
                    int year = 0, month = 0, day = 0;
                    int hour = 0, minute = 0, second = 0;

                    while (_zkemDevice.GetSuperLogData(
                        request.DeviceNumber,
                        ref index,
                        ref adminId,
                        ref operation,
                        ref param1,
                        ref param2,
                        ref param3,
                        ref year,
                        ref month,
                        ref day,
                        ref hour,
                        ref minute,
                        ref second))
                    {
                        try
                        {
                            // Walidacja i korekta daty
                            year = year < 2000 ? 2000 + year : year;  // Jeśli rok jest dwucyfrowy
                            month = Math.Max(1, Math.Min(12, month)); // Miesiąc między 1 a 12
                            day = Math.Max(1, Math.Min(31, day));     // Dzień między 1 a 31
                            hour = Math.Max(0, Math.Min(23, hour));   // Godzina między 0 a 23
                            minute = Math.Max(0, Math.Min(59, minute)); // Minuta między 0 a 59
                            second = Math.Max(0, Math.Min(59, second)); // Sekunda między 0 a 59

                            var dateTime = new DateTime(year, month, day, hour, minute, second);

                            logs.Add(new OperationLog
                            {
                                Index = index,
                                AdminId = adminId,
                                Operation = operation,
                                DateTime = dateTime,
                                WorkCode = $"{param1},{param2},{param3}",
                                OperationName = GetOperationName(operation)
                            });
                        }
                        catch (Exception ex)
                        {
                            // Jeśli data nadal jest nieprawidłowa, używamy aktualnej daty
                            logs.Add(new OperationLog
                            {
                                Index = index,
                                AdminId = adminId,
                                Operation = operation,
                                DateTime = DateTime.Now,
                                WorkCode = $"{param1},{param2},{param3} (Błędna data: {year}-{month}-{day} {hour}:{minute}:{second})",
                                OperationName = GetOperationName(operation)
                            });
                        }
                    }

                    return Ok(new
                    {
                        Success = true,
                        Data = logs
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
                    Message = $"Błąd podczas pobierania logów operacji: {ex.Message}"
                });
            }
        }

        private string GetOperationName(int operation)
        {
            switch (operation)
            {
                case 0: return "Uruchomienie urządzenia";
                case 1: return "Wyłączenie urządzenia";
                case 2: return "Wejście w menu";
                case 3: return "Zmiana ustawień systemowych";
                case 4: return "Zmiana ustawień zaawansowanych";
                case 5: return "Zmiana ustawień komunikacji";
                case 6: return "Zmiana ustawień personalizacji";
                case 7: return "Zmiana ustawień zarządzania użytkownikami";
                case 8: return "Zmiana ustawień kontroli dostępu";
                case 9: return "Aktualizacja firmware";
                case 10: return "Przeglądanie logów systemowych";
                case 11: return "Backup danych";
                case 12: return "Przywrócenie danych";
                case 13: return "Formatowanie urządzenia";
                case 14: return "Dodanie użytkownika";
                case 15: return "Usunięcie użytkownika";
                case 16: return "Zmiana danych użytkownika";
                case 17: return "Dodanie karty";
                case 18: return "Usunięcie karty";
                case 19: return "Czyszczenie logów";
                default: return $"Nieznana operacja ({operation})";
            }
        }
    }

    public class LogRequest
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
    }
} 