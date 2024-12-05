using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        private readonly IZkemDevice _zkemDevice;

        public EmployeeController(IZkemDevice zkemDevice)
        {
            _zkemDevice = zkemDevice;
        }

        /// <summary>
        /// Pobiera dane pracownika z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia i numer ewidencyjny pracownika</param>
        /// <returns>Dane pracownika</returns>
        [HttpPost("get-info")]
        public IActionResult GetEmployeeInfo([FromBody] GetEmployeeRequest request)
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

                    // 3. Pobieranie danych pracownika
                    string name = string.Empty;
                    string password = string.Empty;
                    string cardNumber = string.Empty;
                    int privilege = 0;
                    bool enabled = false;

                    bool success = _zkemDevice.SSR_GetUserInfo(
                        request.DeviceNumber,
                        request.EnrollNumber,
                        out name,
                        out password,
                        out privilege,
                        out enabled);

                    if (!success)
                    {
                        return NotFound(new
                        {
                            Success = false,
                            Message = "Nie znaleziono pracownika o podanym numerze ewidencyjnym"
                        });
                    }

                    // 4. Pobieranie numeru karty
                    _zkemDevice.GetStrCardNumber(out cardNumber);

                    var userInfo = new UserInfo
                    {
                        EnrollNumber = request.EnrollNumber,
                        Name = name,
                        CardNumber = cardNumber,
                        Password = password,
                        Privilege = privilege,
                        Enabled = enabled
                    };

                    return Ok(new
                    {
                        Success = true,
                        Data = userInfo
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
                    Message = $"Błąd podczas pobierania danych pracownika: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Pobiera dane wszystkich pracowników z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia</param>
        /// <returns>Lista danych pracowników</returns>
        [HttpPost("get-all")]
        public IActionResult GetAllEmployees([FromBody] DeviceRequest request)
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

                    var employees = new List<UserInfo>();

                    // 3. Pobieranie danych wszystkich pracowników
                    bool success = _zkemDevice.ReadAllUserID(request.DeviceNumber);
                    if (!success)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Nie udało się odczytać listy pracowników"
                        });
                    }

                    // 4. Pobieranie danych każdego pracownika
                    string enrollNumber = string.Empty;
                    int verifyMode = 0;
                    int inOutMode = 0;
                    int year = 0, month = 0, day = 0, hour = 0, minute = 0, second = 0;

                    while (_zkemDevice.SSR_GetAllUserInfo(
                        request.DeviceNumber,
                        out enrollNumber,
                        out string name,
                        out string password,
                        out int privilege,
                        out bool enabled))
                    {
                        string cardNumber = string.Empty;
                        _zkemDevice.GetStrCardNumber(out cardNumber);

                        employees.Add(new UserInfo
                        {
                            EnrollNumber = enrollNumber,
                            Name = name,
                            CardNumber = cardNumber,
                            Password = password,
                            Privilege = privilege,
                            Enabled = enabled
                        });
                    }

                    return Ok(new
                    {
                        Success = true,
                        Data = employees
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
                    Message = $"Błąd podczas pobierania danych pracowników: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Dodaje lub aktualizuje dane pracownika w czytniku
        /// </summary>
        [HttpPost("save")]
        public IActionResult SaveEmployee([FromBody] EmployeeRequest request)
        {
            try
            {
                // Sprawdzamy czy numer karty nie jest pusty i ma więcej niż 1 znak
                if (!string.IsNullOrEmpty(request.CardNumber) && request.CardNumber.Length > 1)
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
                        _zkemDevice.EnableDevice(request.DeviceNumber, false);

                        // 2. Pobieramy listę wszystkich pracowników
                        bool success = _zkemDevice.ReadAllUserID(request.DeviceNumber);
                        if (!success)
                        {
                            return BadRequest(new
                            {
                                Success = false,
                                Message = "Nie udało się odczytać listy pracowników"
                            });
                        }

                        // 3. Sprawdzamy każdego pracownika
                        string enrollNumber = string.Empty;
                        while (_zkemDevice.SSR_GetAllUserInfo(
                            request.DeviceNumber,
                            out enrollNumber,
                            out string name,
                            out string password,
                            out int privilege,
                            out bool enabled))
                        {
                            // Pomijamy sprawdzanego pracownika (w przypadku aktualizacji)
                            if (enrollNumber == request.EnrollNumber)
                                continue;

                            string cardNumber = string.Empty;
                            _zkemDevice.GetStrCardNumber(out cardNumber);

                            if (cardNumber == request.CardNumber)
                            {
                                return BadRequest(new
                                {
                                    Success = false,
                                    Message = $"Karta o numerze {request.CardNumber} jest już przypisana do pracownika {name} (numer: {enrollNumber})"
                                });
                            }
                        }

                        // 4. Sprawdzamy czy pracownik istnieje
                        string existingName = string.Empty;
                        string existingPassword = string.Empty;
                        int existingPrivilege = 0;
                        bool existingEnabled = false;

                        bool exists = _zkemDevice.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            out existingName,
                            out existingPassword,
                            out existingPrivilege,
                            out existingEnabled);

                        // 5. Zapisujemy dane
                        success = _zkemDevice.SSR_SetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            request.Name,
                            request.Password,
                            request.Privilege,
                            request.Enabled,
                            request.CardNumber);

                        if (!success)
                        {
                            return BadRequest(new
                            {
                                Success = false,
                                Message = "Nie udało się zapisać danych pracownika"
                            });
                        }

                        return Ok(new
                        {
                            Success = true,
                            Message = exists ? 
                                "Dane pracownika zostały zaktualizowane pomyślnie" : 
                                "Pracownik został dodany pomyślnie",
                            IsNewEmployee = !exists
                        });
                    }
                    finally
                    {
                        _zkemDevice.EnableDevice(request.DeviceNumber, true);
                        _zkemDevice.Disconnect();
                    }
                }
                else
                {
                    // Jeśli karta jest pusta lub ma 1 znak, wykonujemy standardowe zapisywanie
                    return SaveEmployeeWithoutCardCheck(request);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas zapisywania danych pracownika: {ex.Message}"
                });
            }
        }

        private IActionResult SaveEmployeeWithoutCardCheck(EmployeeRequest request)
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

                    // Sprawdzamy czy pracownik istnieje
                    string existingName = string.Empty;
                    string existingPassword = string.Empty;
                    int existingPrivilege = 0;
                    bool existingEnabled = false;

                    bool exists = _zkemDevice.SSR_GetUserInfo(
                        request.DeviceNumber,
                        request.EnrollNumber,
                        out existingName,
                        out existingPassword,
                        out existingPrivilege,
                        out existingEnabled);

                    // Zapisujemy dane
                    bool success = _zkemDevice.SSR_SetUserInfo(
                        request.DeviceNumber,
                        request.EnrollNumber,
                        request.Name,
                        request.Password,
                        request.Privilege,
                        request.Enabled,
                        request.CardNumber);

                    if (!success)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Nie udało się zapisać danych pracownika"
                        });
                    }

                    return Ok(new
                    {
                        Success = true,
                        Message = exists ? 
                            "Dane pracownika zostały zaktualizowane pomyślnie" : 
                            "Pracownik został dodany pomyślnie",
                        IsNewEmployee = !exists
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
                    Message = $"Błąd podczas zapisywania danych pracownika: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Zmienia numer ewidencyjny pracownika
        /// </summary>
        [HttpPost("change-enroll")]
        public IActionResult ChangeEnrollNumber([FromBody] ChangeEnrollRequest request)
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

                    // 1. Pobieramy aktualne dane pracownika
                    string name = string.Empty;
                    string password = string.Empty;
                    string cardNumber = string.Empty;
                    int privilege = 0;
                    bool enabled = false;

                    bool exists = _zkemDevice.SSR_GetUserInfo(
                        request.DeviceNumber,
                        request.OldEnrollNumber,
                        out name,
                        out password,
                        out privilege,
                        out enabled);

                    if (!exists)
                    {
                        return NotFound(new
                        {
                            Success = false,
                            Message = "Nie znaleziono pracownika o podanym starym numerze ewidencyjnym"
                        });
                    }

                    // 2. Sprawdzamy czy nowy numer nie jest już zajęty
                    exists = _zkemDevice.SSR_GetUserInfo(
                        request.DeviceNumber,
                        request.NewEnrollNumber,
                        out _, out _, out _, out _);

                    if (exists)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Podany nowy numer ewidencyjny jest już zajęty"
                        });
                    }

                    // 3. Pobieramy numer karty
                    _zkemDevice.GetStrCardNumber(out cardNumber);

                    // 4. Dodajemy pracownika z nowym numerem
                    bool success = _zkemDevice.SSR_SetUserInfo(
                        request.DeviceNumber,
                        request.NewEnrollNumber,
                        name,
                        password,
                        privilege,
                        enabled,
                        cardNumber);

                    if (!success)
                    {
                        return BadRequest(new
                        {
                            Success = false,
                            Message = "Nie udało się utworzyć nowego wpisu dla pracownika"
                        });
                    }

                    // 5. Usuwamy stary wpis
                    success = _zkemDevice.SSR_DeleteEnrollData(
                        request.DeviceNumber,
                        request.OldEnrollNumber,
                        12);  // 12 = wszystkie dane użytkownika

                    if (!success)
                    {
                        return StatusCode(500, new
                        {
                            Success = false,
                            Message = "Nie udało się usunąć starego wpisu pracownika"
                        });
                    }

                    return Ok(new
                    {
                        Success = true,
                        Message = "Numer ewidencyjny pracownika został zmieniony pomyślnie"
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
                    Message = $"Błąd podczas zmiany numeru ewidencyjnego: {ex.Message}"
                });
            }
        }
    }

    public class GetEmployeeRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        public required string EnrollNumber { get; set; }
    }

    public class DeviceRequest
    {
        public required string IpAddress { get; set; }
        public required int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
    }

    public class EmployeeRequest : DeviceRequest
    {
        public required string EnrollNumber { get; set; }
        public  string? Name { get; set; }
        public string? Password { get; set; }
        public string? CardNumber { get; set; }
        public int Privilege { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class ChangeEnrollRequest : DeviceRequest
    {
        public required string OldEnrollNumber { get; set; }
        public required string NewEnrollNumber { get; set; }
    }
} 