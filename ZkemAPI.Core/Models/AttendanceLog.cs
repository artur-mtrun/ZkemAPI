using System;

namespace ZkemAPI.Core.Models
{
    public class AttendanceLog
    {
        public string UserId { get; set; }
        public DateTime LogTime { get; set; }
        public VerifyMode VerifyMode { get; set; }
        public InOutMode InOutMode { get; set; }
        public int WorkCode { get; set; }
    }

    public enum VerifyMode
    {
        Password = 0,
        Fingerprint = 1,
        Card = 2,
        OnlyId = 3,
        PasswordAndFingerprint = 4,
        FingerprintAndCard = 5,
        PasswordAndCard = 6,
        All = 7,
        IdAndFingerprint = 8,
        FingerprintOrCard = 9,
        IdAndCard = 10
    }

    public enum InOutMode
    {
        In = 0,
        Out = 1,
        Break = 2,
        ReturnFromBreak = 3,
        Overtime = 4,
        EndOvertime = 5
    }
} 