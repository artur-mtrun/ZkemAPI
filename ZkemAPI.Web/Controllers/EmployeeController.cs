using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployeeController : ControllerBase
    {
        private readonly IDeviceConnectionManager _deviceManager;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(IDeviceConnectionManager deviceManager, ILogger<EmployeeController> logger)
        {
            _deviceManager = deviceManager;
            _logger = logger;
        }

        /// <summary>
        /// Pobiera dane pracownika z czytnika
        /// </summary>
        /// <param name="request">Parametry połączenia i numer ewidencyjny pracownika</param>
        /// <returns>Dane pracownika</returns>
        [HttpPost("get-info")]
        public async Task<IActionResult> GetEmployeeInfo([FromBody] GetEmployeeRequest request)
        {
            _logger.LogInformation("Rozpoczęcie pobierania danych pracownika {EnrollNumber} z czytnika {IpAddress}:{Port}", 
                request.EnrollNumber, request.IpAddress, request.Port);
            
            try
            {
                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    _logger.LogDebug("Próba połączenia z czytnikiem {IpAddress}:{Port}", request.IpAddress, request.Port);
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        _logger.LogError("Nie udało się połączyć z czytnikiem {IpAddress}:{Port}", request.IpAddress, request.Port);
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        // Blokowanie urządzenia
                        device.EnableDevice(request.DeviceNumber, false);

                        // Pobieranie danych pracownika
                        string name = string.Empty;
                        string password = string.Empty;
                        string cardNumber = string.Empty;
                        int privilege = 0;
                        bool enabled = false;

                        bool success = device.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            out name,
                            out password,
                            out privilege,
                            out enabled);

                        if (!success)
                        {
                            throw new KeyNotFoundException("Nie znaleziono pracownika o podanym numerze ewidencyjnym");
                        }

                        // Pobieranie numeru karty
                        device.GetStrCardNumber(out cardNumber);

                        return new UserInfo
                        {
                            EnrollNumber = request.EnrollNumber,
                            Name = name,
                            CardNumber = cardNumber,
                            Password = password,
                            Privilege = privilege,
                            Enabled = enabled
                        };
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
                    Data = result
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania danych pracownika {EnrollNumber} z czytnika {IpAddress}:{Port}", 
                    request.EnrollNumber, request.IpAddress, request.Port);
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
        public async Task<IActionResult> GetAllEmployees([FromBody] DeviceRequest request)
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

                        var employees = new List<UserInfo>();

                        // Pobieranie danych wszystkich pracowników
                        bool success = device.ReadAllUserID(request.DeviceNumber);
                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się odczytać listy pracowników");
                        }

                        // Iteracja przez wszystkich użytkowników
                        string enrollNumber, name, password;
                        int privilege;
                        bool enabled;

                        while (device.SSR_GetAllUserInfo(request.DeviceNumber, out enrollNumber, out name, 
                               out password, out privilege, out enabled))
                        {
                            string cardNumber = string.Empty;
                            device.GetStrCardNumber(out cardNumber);

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

                        return employees.OrderBy(x => int.TryParse(x.EnrollNumber, out int num) ? num : int.MaxValue);
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
                    Data = result
                });
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
        public async Task<IActionResult> SaveEmployee([FromBody] EmployeeRequest request)
        {
            try
            {
                // Sprawdzamy czy numer karty nie jest pusty i ma więcej niż 1 znak
                if (!string.IsNullOrEmpty(request.CardNumber) && request.CardNumber.Length > 1)
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
                            device.EnableDevice(request.DeviceNumber, false);

                            // Pobieramy listę wszystkich pracowników
                            bool success = device.ReadAllUserID(request.DeviceNumber);
                            if (!success)
                            {
                                throw new InvalidOperationException("Nie udało się odczytać listy pracowników");
                            }

                            // Sprawdzamy czy karta nie jest już przypisana do innego użytkownika
                            string enrollNumber, name, password;
                            int privilege;
                            bool enabled;

                            while (device.SSR_GetAllUserInfo(request.DeviceNumber, out enrollNumber, out name, 
                                   out password, out privilege, out enabled))
                            {
                                string existingCardNumber = string.Empty;
                                device.GetStrCardNumber(out existingCardNumber);

                                if (!string.IsNullOrEmpty(existingCardNumber) && 
                                    existingCardNumber.Equals(request.CardNumber, StringComparison.OrdinalIgnoreCase) &&
                                    enrollNumber != request.EnrollNumber)
                                {
                                    throw new InvalidOperationException($"Karta {request.CardNumber} jest już przypisana do pracownika {enrollNumber} ({name})");
                                }
                            }

                            // Sprawdzamy czy pracownik istnieje
                            string existingName = string.Empty;
                            string existingPassword = string.Empty;
                            int existingPrivilege = 0;
                            bool existingEnabled = false;

                            bool exists = device.SSR_GetUserInfo(
                                request.DeviceNumber,
                                request.EnrollNumber,
                                out existingName,
                                out existingPassword,
                                out existingPrivilege,
                                out existingEnabled);

                            // Zapisywanie danych pracownika z kartą
                            bool saveSuccess = device.SSR_SetUserInfo(
                                request.DeviceNumber,
                                request.EnrollNumber,
                                request.Name,
                                request.Password ?? string.Empty,
                                request.Privilege,
                                request.Enabled,
                                request.CardNumber);

                            if (!saveSuccess)
                            {
                                throw new InvalidOperationException("Nie udało się zapisać danych pracownika");
                            }

                            return exists ? "Dane pracownika zostały zaktualizowane pomyślnie" : "Pracownik został dodany pomyślnie";
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
                        Message = result
                    });
                }
                else
                {
                    // Jeśli karta jest pusta lub ma 1 znak, wykonujemy standardowe zapisywanie
                    return await SaveEmployeeWithoutCardCheck(request);
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

        private async Task<IActionResult> SaveEmployeeWithoutCardCheck(EmployeeRequest request)
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

                        // Sprawdzamy czy pracownik istnieje
                        string existingName = string.Empty;
                        string existingPassword = string.Empty;
                        int existingPrivilege = 0;
                        bool existingEnabled = false;

                        bool exists = device.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            out existingName,
                            out existingPassword,
                            out existingPrivilege,
                            out existingEnabled);

                        // Zapisywanie danych pracownika
                        bool success = device.SSR_SetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            request.Name,
                            request.Password ?? string.Empty,
                            request.Privilege,
                            request.Enabled,
                            request.CardNumber ?? string.Empty);

                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się zapisać danych pracownika");
                        }

                        return exists ? "Dane pracownika zostały zaktualizowane pomyślnie" : "Pracownik został dodany pomyślnie";
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
                    Message = result
                });
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
        public async Task<IActionResult> ChangeEnrollNumber([FromBody] ChangeEnrollRequest request)
        {
            try
            {
                await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        device.EnableDevice(request.DeviceNumber, false);

                        // Pobieramy aktualne dane pracownika
                        string name = string.Empty;
                        string password = string.Empty;
                        string cardNumber = string.Empty;
                        int privilege = 0;
                        bool enabled = false;

                        bool exists = device.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.OldEnrollNumber,
                            out name,
                            out password,
                            out privilege,
                            out enabled);

                        if (!exists)
                        {
                            throw new KeyNotFoundException("Nie znaleziono pracownika o podanym starym numerze ewidencyjnym");
                        }

                        // Sprawdzamy czy nowy numer nie jest już zajęty
                        exists = device.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.NewEnrollNumber,
                            out _, out _, out _, out _);

                        if (exists)
                        {
                            throw new InvalidOperationException("Nowy numer ewidencyjny jest już zajęty");
                        }

                        // Pobieramy numer karty
                        device.GetStrCardNumber(out cardNumber);

                        // Usuwamy starego pracownika
                        device.SSR_DeleteEnrollData(request.DeviceNumber, request.OldEnrollNumber, 11);

                        // Dodajemy pracownika z nowym numerem
                        bool success = device.SSR_SetUserInfo(
                            request.DeviceNumber,
                            request.NewEnrollNumber,
                            name,
                            password,
                            privilege,
                            enabled,
                            cardNumber);

                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się utworzyć pracownika z nowym numerem ewidencyjnym");
                        }
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
                    Message = "Numer ewidencyjny pracownika został zmieniony pomyślnie"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = ex.Message
                });
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

        /// <summary>
        /// Dodaje lub aktualizuje dane wielu pracowników w czytniku
        /// </summary>
        [HttpPost("save-batch")]
        public async Task<IActionResult> SaveEmployees([FromBody] BatchEmployeeRequest request)
        {
            try
            {
                if (!request.Employees.Any())
                {
                    return BadRequest(new
                    {
                        Success = false,
                        Message = "Lista pracowników jest pusta"
                    });
                }

                var result = await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
                {
                    // Połączenie z czytnikiem (tylko raz)
                    if (!device.Connect_Net(request.IpAddress, request.Port))
                    {
                        throw new InvalidOperationException("Nie udało się połączyć z czytnikiem");
                    }

                    try
                    {
                        device.EnableDevice(request.DeviceNumber, false);
                        var results = new List<object>();

                        foreach (var employee in request.Employees)
                        {
                            try
                            {
                                // Sprawdzamy czy pracownik istnieje
                                string existingName = string.Empty;
                                string existingPassword = string.Empty;
                                int existingPrivilege = 0;
                                bool existingEnabled = false;

                                bool exists = device.SSR_GetUserInfo(
                                    request.DeviceNumber,
                                    employee.EnrollNumber,
                                    out existingName,
                                    out existingPassword,
                                    out existingPrivilege,
                                    out existingEnabled);

                                // Zapisywanie danych pracownika
                                bool success = device.SSR_SetUserInfo(
                                    request.DeviceNumber,
                                    employee.EnrollNumber,
                                    employee.Name,
                                    employee.Password ?? string.Empty,
                                    employee.Privilege,
                                    employee.Enabled,
                                    employee.CardNumber ?? string.Empty);

                                if (success)
                                {
                                    results.Add(new
                                    {
                                        EnrollNumber = employee.EnrollNumber,
                                        Success = true,
                                        Message = exists ? "Zaktualizowano" : "Dodano",
                                        Action = exists ? "Updated" : "Added"
                                    });
                                }
                                else
                                {
                                    results.Add(new
                                    {
                                        EnrollNumber = employee.EnrollNumber,
                                        Success = false,
                                        Message = "Nie udało się zapisać danych pracownika",
                                        Action = "Failed"
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                results.Add(new
                                {
                                    EnrollNumber = employee.EnrollNumber,
                                    Success = false,
                                    Message = ex.Message,
                                    Action = "Error"
                                });
                            }
                        }

                        return results;
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
                    Message = "Operacja grupowa zakończona",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas zapisywania danych pracowników: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Usuwa pracownika z czytnika
        /// </summary>
        [HttpPost("delete")]
        public async Task<IActionResult> DeleteEmployee([FromBody] GetEmployeeRequest request)
        {
            try
            {
                await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
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

                        // Sprawdzamy czy pracownik istnieje
                        string name = string.Empty;
                        string password = string.Empty;
                        int privilege = 0;
                        bool enabled = false;

                        bool exists = device.SSR_GetUserInfo(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            out name,
                            out password,
                            out privilege,
                            out enabled);

                        if (!exists)
                        {
                            throw new KeyNotFoundException("Nie znaleziono pracownika o podanym numerze ewidencyjnym");
                        }

                        // Usuwamy pracownika
                        bool success = device.SSR_DeleteEnrollData(
                            request.DeviceNumber,
                            request.EnrollNumber,
                            12); // 12 = wszystkie dane użytkownika

                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się usunąć pracownika");
                        }
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
                    Message = "Pracownik został usunięty pomyślnie"
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas usuwania pracownika: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Usuwa wszystkich pracowników z czytnika
        /// </summary>
        [HttpPost("delete-all")]
        public async Task<IActionResult> DeleteAllEmployees([FromBody] DeviceRequest request)
        {
            try
            {
                await _deviceManager.ExecuteDeviceOperationAsync(request.IpAddress, request.Port, device =>
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

                        // Usuwamy wszystkich pracowników (5 = wszystkie dane użytkowników)
                        bool success = device.ClearData(request.DeviceNumber, 5);
                        
                        if (!success)
                        {
                            throw new InvalidOperationException("Nie udało się usunąć danych pracowników");
                        }
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
                    Message = "Wszyscy pracownicy zostali usunięci pomyślnie"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas usuwania pracowników: {ex.Message}"
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

    /// <summary>
    /// Nowe klasy dla batch requestu
    /// </summary>
    public class BatchEmployeeRequest : DeviceRequest
    {
        public required List<BatchEmployee> Employees { get; set; }
    }

    public class BatchEmployee
    {
        public required string EnrollNumber { get; set; }
        public string? Name { get; set; }
        public string? Password { get; set; }
        public string? CardNumber { get; set; }
        public int Privilege { get; set; }
        public bool Enabled { get; set; } = true;
    }
    

}