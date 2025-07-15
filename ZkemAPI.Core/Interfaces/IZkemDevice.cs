using System.Threading.Tasks;

namespace ZkemAPI.Core.Interfaces
{
    public interface IZkemDevice
    {
        // Połączenie
        bool Connect_Net(string IPAdd, int Port);
        void Disconnect();
        
        // Zarządzanie czasem
        bool GetDeviceTime(int dwMachineNumber, ref int dwYear, ref int dwMonth, ref int dwDay, ref int dwHour, ref int dwMinute, ref int dwSecond);
        bool SetDeviceTime2(int dwMachineNumber, int dwYear, int dwMonth, int dwDay, int dwHour, int dwMinute, int dwSecond);
        
        // Zarządzanie kartami RFID
        bool GetStrCardNumber(out string ACardNumber);
        bool SetStrCardNumber(string ACardNumber);
        bool GetCardFun(int dwMachineNumber, ref int CardFun);
        
        // Zarządzanie użytkownikami - stare API
        bool GetUserInfo(int dwMachineNumber, int dwEnrollNumber, ref string name, ref string password, ref int privilege, ref bool enabled);
        bool SetUserInfo(int dwMachineNumber, int dwEnrollNumber, string name, string password, int privilege, bool enabled);
        bool DeleteUserInfoEx(int dwMachineNumber, int dwEnrollNumber);
        bool GetUserInfoEx(int dwMachineNumber, int dwEnrollNumber, out int dwVerifyMode, out byte Reserved);
        
        // Zarządzanie użytkownikami - nowe API (SSR)
        bool SSR_GetUserInfo(int dwMachineNumber, string enrollNumber, out string name, 
            out string password, out int privilege, out bool enabled);
        bool SSR_SetUserInfo(int dwMachineNumber, string enrollNumber, string name, string password, int privilege, bool enabled, string cardNumber);
        bool SSR_DeleteEnrollData(int dwMachineNumber, string enrollNumber, int backupNumber);
        
        // Pobieranie zdarzeń (logów) - stare API
        bool GetGeneralLogData(int dwMachineNumber, ref int dwTMachineNumber, ref int dwEnrollNumber, 
            ref int dwEMachineNumber, ref int dwVerifyMode, ref int dwInOutMode, ref int dwYear, ref int dwMonth, 
            ref int dwDay, ref int dwHour, ref int dwMinute);
        bool ReadGeneralLogData(int dwMachineNumber);
        bool ReadTimeGLogData(int dwMachineNumber, string sTime, string eTime);
        
        // Pobieranie zdarzeń (logów) - nowe API (SSR)
        bool SSR_GetGeneralLogData(int dwMachineNumber, out string enrollNumber, out int verifyMode, 
            out int inOutMode, out int year, out int month, out int day, out int hour, out int minute, 
            out int second, out int workCode);
        
        // Zarządzanie danymi
        bool ReadAllUserID(int dwMachineNumber);
        bool RefreshData(int dwMachineNumber);
        bool ClearGLog(int dwMachineNumber);
        
        // Operacje na urządzeniu
        bool EnableDevice(int dwMachineNumber, bool enabled);
        bool RestartDevice(int dwMachineNumber);

        /// <summary>
        /// Pobiera informacje o wszystkich użytkownikach
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        bool GetAllUserID(int dwMachineNumber);

        /// <summary>
        /// Pobiera szczegółowe informacje o wszystkich użytkownikach
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <returns>Krotka zawierająca: (sukces, numer wpisu, numer urządzenia, numer kopii, uprawnienia, aktywność)</returns>
        (bool success, int enrollNumber, int machineNumber, int backupNumber, int privilege, bool enabled) 
            GetAllUserIDEx(int dwMachineNumber);

        bool SSR_GetAllUserInfo(int dwMachineNumber, out string enrollNumber, out string name, 
            out string password, out int privilege, out bool enabled);

        // Informacje o urządzeniu
        bool GetFirmwareVersion(int dwMachineNumber, ref string strVersion);
        bool GetDeviceMAC(int dwMachineNumber, ref string strMAC);
        bool GetPlatform(int dwMachineNumber, ref string strPlatform);
        bool GetSerialNumber(int dwMachineNumber, ref string strSerialNumber);
        bool GetDeviceStrInfo(int dwMachineNumber, int dwInfo, out string stringValue);
        bool GetDeviceInfo(int dwMachineNumber, int dwInfo, ref int dwValue);
        bool GetDeviceStatus(int dwMachineNumber, int dwStatus, ref int dwValue);

        bool GetSuperLogData(
            int dwMachineNumber,
            ref int dwTMachineNumber,
            ref int dwSEnrollNumber,
            ref int dwParams4,
            ref int dwParams1,
            ref int dwParams2,
            ref int dwParams3,
            ref int dwYear,
            ref int dwMonth,
            ref int dwDay,
            ref int dwHour,
            ref int dwMinute,
            ref int dwSecond);

        bool ReadAllTemplate(int dwMachineNumber);
        bool GetUserTmpExStr(int dwMachineNumber, string dwEnrollNumber, int dwFingerIndex, out int flag, out string tmpData, out int tmpLength);
        bool SetUserTmpExStr(int dwMachineNumber, string dwEnrollNumber, int dwFingerIndex, int flag, string tmpData);

        bool ClearData(int dwMachineNumber, int dwValue);

        // Sygnały dźwiękowe
        bool Beep(int delay);
        bool PlayVoice(int voiceIndex, int length);
    }
} 