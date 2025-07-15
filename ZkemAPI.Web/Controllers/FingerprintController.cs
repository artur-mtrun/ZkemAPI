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
    public class FingerprintController : ControllerBase
    {
        private readonly IDeviceConnectionManager _deviceManager;

        public FingerprintController(IDeviceConnectionManager deviceManager)
        {
            _deviceManager = deviceManager;
        }

        /// <summary>
        /// Pobiera odciski palców użytkownika
        /// </summary>
        [HttpPost("get")]
        public async Task<IActionResult> GetFingerprints([FromBody] FingerprintRequest request)
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

                    var fingerprints = new List<FingerprintData>();
                        device.ReadAllTemplate(request.DeviceNumber);

                        // Sprawdzamy każdy możliwy palec (0-9)
                        for (int fingerIndex = 0; fingerIndex < 10; fingerIndex++)
                    {
                            if (device.GetUserTmpExStr(request.DeviceNumber, request.EnrollNumber, fingerIndex, 
                                out int flag, out string tmpData, out int tmpLength))
                        {
                            fingerprints.Add(new FingerprintData
                            {
                                    FingerIndex = fingerIndex,
                                Flag = flag,
                                TemplateData = tmpData,
                                TemplateLength = tmpLength
                            });
                        }
                    }

                        return fingerprints;
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
                    Message = $"Błąd podczas pobierania odcisków palców: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Wysyła odciski palców użytkownika
        /// </summary>
        [HttpPost("set")]
        public async Task<IActionResult> SetFingerprints([FromBody] FingerprintSetRequest request)
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

                    foreach (var fingerprint in request.Fingerprints)
                    {
                            if (!device.SetUserTmpExStr(
                            request.DeviceNumber, 
                            request.EnrollNumber, 
                            fingerprint.FingerIndex,
                            fingerprint.Flag,
                            fingerprint.TemplateData))
                        {
                                throw new InvalidOperationException($"Nie udało się zapisać odcisku palca dla palca {fingerprint.FingerIndex}");
                        }
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
                        Message = "Odciski palców zostały zapisane pomyślnie"
                    });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas zapisywania odcisków palców: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Wysyła odciski palców dla wielu użytkowników
        /// </summary>
        [HttpPost("bulk-set")]
        public async Task<IActionResult> SetBulkFingerprints([FromBody] FingerprintBulkSetRequest request)
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

                    var results = new List<BulkOperationResult>();

                    foreach (var userFingerprints in request.UsersFingerprints)
                    {
                        var userResult = new BulkOperationResult
                        {
                            EnrollNumber = userFingerprints.EnrollNumber,
                            Success = true,
                            Errors = new List<string>()
                        };

                            try
                            {
                        foreach (var fingerprint in userFingerprints.Fingerprints)
                        {
                                    if (!device.SetUserTmpExStr(
                                request.DeviceNumber,
                                userFingerprints.EnrollNumber,
                                fingerprint.FingerIndex,
                                fingerprint.Flag,
                                fingerprint.TemplateData))
                            {
                                userResult.Success = false;
                                userResult.Errors.Add($"Nie udało się zapisać odcisku palca {fingerprint.FingerIndex}");
                            }
                                }
                            }
                            catch (Exception ex)
                            {
                                userResult.Success = false;
                                userResult.Errors.Add($"Błąd dla użytkownika {userFingerprints.EnrollNumber}: {ex.Message}");
                        }

                        results.Add(userResult);
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
                    Message = "Operacja masowego wysyłania odcisków palców zakończona",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Błąd podczas masowego zapisywania odcisków palców: {ex.Message}"
                });
            }
        }
    }

    public class FingerprintRequest
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        public string EnrollNumber { get; set; }
    }

    public class FingerprintSetRequest : FingerprintRequest
    {
        public List<FingerprintData> Fingerprints { get; set; }
    }

    public class FingerprintData
    {
        public int FingerIndex { get; set; }
        public int Flag { get; set; }
        public string TemplateData { get; set; }
        public int TemplateLength { get; set; }
    }

    public class FingerprintBulkSetRequest
    {
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public int DeviceNumber { get; set; } = 1;
        public List<UserFingerprints> UsersFingerprints { get; set; }
    }

    public class UserFingerprints
    {
        public string EnrollNumber { get; set; }
        public List<FingerprintData> Fingerprints { get; set; }
    }

    public class BulkOperationResult
    {
        public string EnrollNumber { get; set; }
        public bool Success { get; set; }
        public List<string> Errors { get; set; }
    }
}