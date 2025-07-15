using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly IDeviceConnectionManager _deviceManager;

        public LogController(IDeviceConnectionManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        /// <summary>
        /// Pobiera logi operacji administracyjnych
        /// </summary>
        [HttpPost("operations")]
        public async Task<IActionResult> GetOperationLogs([FromBody] LogRequest request)
        {
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                }

                try
                {
                        device.EnableDevice(request.DeviceNumber, false);

                    var logs = new List<OperationLog>();

                    int index = 0;
                    int adminId = 0;
                    int operation = 0;
                    int param1 = 0, param2 = 0, param3 = 0;
                    int year = 0, month = 0, day = 0;
                    int hour = 0, minute = 0, second = 0;

                        // Pobieranie logów operacji jeden po drugim
                        while (device.GetSuperLogData(
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
                            logs.Add(new OperationLog
                            {
                                Index = index,
                                AdminId = adminId,
                                Operation = operation,
                                OperationName = GetOperationName(operation),
                                WorkCode = $"{param1},{param2},{param3}",
                                DateTime = new DateTime(year, month, day, hour, minute, second)
                            });
                        }

                        return logs.OrderByDescending(x => x.DateTime);
                    }
                    finally
                    {
                        device.EnableDevice(request.DeviceNumber, true);
                        device.Disconnect();
                        }
                });

                    return Ok(new
                    {
                        Success = true,
                    Data = result
                    });
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
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
    }
} 