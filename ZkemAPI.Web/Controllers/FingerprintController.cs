using Microsoft.AspNetCore.Mvc;
using ZkemAPI.Core.Interfaces;
using ZkemAPI.Core.Models;
using System;
using System.Collections.Generic;

namespace ZkemAPI.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FingerprintController : ControllerBase
    {
        private readonly IZkemDevice _zkemDevice;

        public FingerprintController(IZkemDevice zkemDevice)
        {
            _zkemDevice = zkemDevice;
        }

        /// <summary>
        /// Pobiera odciski palców użytkownika
        /// </summary>
        [HttpPost("get")]
        public IActionResult GetFingerprints([FromBody] FingerprintRequest request)
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

                    var fingerprints = new List<FingerprintData>();
                    _zkemDevice.ReadAllTemplate(request.DeviceNumber);

                    for (int i = 0; i < 10; i++) // 10 palców
                    {
                        if (_zkemDevice.GetUserTmpExStr(
                            request.DeviceNumber, 
                            request.EnrollNumber, 
                            i, 
                            out int flag,
                            out string tmpData, 
                            out int tmpLength))
                        {
                            fingerprints.Add(new FingerprintData
                            {
                                FingerIndex = i,
                                Flag = flag,
                                TemplateData = tmpData,
                                TemplateLength = tmpLength
                            });
                        }
                    }

                    return Ok(new
                    {
                        Success = true,
                        Data = fingerprints
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
                    Message = $"Błąd podczas pobierania odcisków palców: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Wysyła odciski palców użytkownika
        /// </summary>
        [HttpPost("set")]
        public IActionResult SetFingerprints([FromBody] FingerprintSetRequest request)
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

                    foreach (var fingerprint in request.Fingerprints)
                    {
                        if (!_zkemDevice.SetUserTmpExStr(
                            request.DeviceNumber, 
                            request.EnrollNumber, 
                            fingerprint.FingerIndex,
                            fingerprint.Flag,
                            fingerprint.TemplateData))
                        {
                            return BadRequest(new
                            {
                                Success = false,
                                Message = $"Nie udało się zapisać odcisku palca dla palca {fingerprint.FingerIndex}"
                            });
                        }
                    }

                    return Ok(new
                    {
                        Success = true,
                        Message = "Odciski palców zostały zapisane pomyślnie"
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
                    Message = $"Błąd podczas zapisywania odcisków palców: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Wysyła odciski palców dla wielu użytkowników
        /// </summary>
        [HttpPost("bulk-set")]
        public IActionResult SetBulkFingerprints([FromBody] FingerprintBulkSetRequest request)
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

                    var results = new List<BulkOperationResult>();

                    foreach (var userFingerprints in request.UsersFingerprints)
                    {
                        var userResult = new BulkOperationResult
                        {
                            EnrollNumber = userFingerprints.EnrollNumber,
                            Success = true,
                            Errors = new List<string>()
                        };

                        foreach (var fingerprint in userFingerprints.Fingerprints)
                        {
                            if (!_zkemDevice.SetUserTmpExStr(
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

                        results.Add(userResult);
                    }

                    return Ok(new
                    {
                        Success = true,
                        Data = results
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