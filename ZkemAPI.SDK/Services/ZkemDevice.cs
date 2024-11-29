using System;
using System.Threading.Tasks;
using zkemkeeper;
using ZkemAPI.Core.Interfaces;

namespace ZkemAPI.SDK.Services
{
    public class ZkemDevice : IZkemDevice
    {
        private readonly CZKEM _sdk;

        // Stałe wartości używane w różnych metodach
        private const int BACKUP_NUMBER_ALL = 11;
        private const int BACKUP_NUMBER_DEFAULT = 3;

        public ZkemDevice()
        {
            _sdk = new CZKEM();
        }

        // Połączenie
        public bool Connect_Net(string IPAdd, int Port) => _sdk.Connect_Net(IPAdd, Port);
        public void Disconnect() => _sdk.Disconnect();

        // Zarządzanie czasem
        public bool GetDeviceTime(int dwMachineNumber, ref int dwYear, ref int dwMonth, ref int dwDay, ref int dwHour, ref int dwMinute, ref int dwSecond)
            => _sdk.GetDeviceTime(dwMachineNumber, ref dwYear, ref dwMonth, ref dwDay, ref dwHour, ref dwMinute, ref dwSecond);
            
        public bool SetDeviceTime2(int dwMachineNumber, int dwYear, int dwMonth, int dwDay, int dwHour, int dwMinute, int dwSecond)
            => _sdk.SetDeviceTime2(dwMachineNumber, dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond);

        // Zarządzanie kartami RFID
        public bool GetStrCardNumber(out string ACardNumber)
            => _sdk.GetStrCardNumber(out ACardNumber);
            
        public bool SetStrCardNumber(string ACardNumber)
            => _sdk.SetStrCardNumber(ACardNumber);
            
        public bool GetCardFun(int dwMachineNumber, ref int CardFun)
        {
            int cardFunRef = 0;
            bool result = _sdk.GetCardFun(dwMachineNumber, ref cardFunRef);
            CardFun = cardFunRef;
            return result;
        }

        // Zarządzanie użytkownikami
        public bool GetUserInfo(int dwMachineNumber, int dwEnrollNumber, ref string name, ref string password, ref int privilege, ref bool enabled)
            => _sdk.GetUserInfo(dwMachineNumber, dwEnrollNumber, ref name, ref password, ref privilege, ref enabled);
            
        public bool SetUserInfo(int dwMachineNumber, int dwEnrollNumber, string name, string password, int privilege, bool enabled)
            => _sdk.SetUserInfo(dwMachineNumber, dwEnrollNumber, name, password, privilege, enabled);
            
        public bool DeleteUserInfoEx(int dwMachineNumber, int dwEnrollNumber)
            => _sdk.DeleteUserInfoEx(dwMachineNumber, dwEnrollNumber);
            
        public bool GetAllUserID(int dwMachineNumber)
        {
            int dwEnrollNumber = 0;
            int dwEMachineNumber = 0;
            int dwBackupNumber = 0;
            int dwMachinePrivilege = 0;
            int dwEnabled = 0;

            return _sdk.GetAllUserID(dwMachineNumber, ref dwEnrollNumber, ref dwEMachineNumber, 
                ref dwBackupNumber, ref dwMachinePrivilege, ref dwEnabled);
        }
            
        public (bool success, int enrollNumber, int machineNumber, int backupNumber, int privilege, bool enabled) 
            GetAllUserIDEx(int dwMachineNumber)
        {
            int dwEnrollNumber = 0;
            int dwEMachineNumber = 0;
            int dwBackupNumber = 0;
            int dwMachinePrivilege = 0;
            int dwEnabled = 0;

            var success = _sdk.GetAllUserID(dwMachineNumber, ref dwEnrollNumber, ref dwEMachineNumber, 
                ref dwBackupNumber, ref dwMachinePrivilege, ref dwEnabled);

            return (success, dwEnrollNumber, dwEMachineNumber, dwBackupNumber, dwMachinePrivilege, dwEnabled == 1);
        }
            
        public bool GetUserInfoEx(int dwMachineNumber, int dwEnrollNumber, out int dwVerifyMode, out byte Reserved)
            => _sdk.GetUserInfoEx(dwMachineNumber, dwEnrollNumber, out dwVerifyMode, out Reserved);

        /// <summary>
        /// Ustawia rozszerzone informacje o użytkowniku (nowa wersja API)
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <param name="enrollNumber">Numer ID użytkownika</param>
        /// <param name="name">Nazwa użytkownika (max 24 znaki)</param>
        /// <param name="password">Hasło użytkownika (max 8 znaków)</param>
        /// <param name="privilege">Uprawnienia:
        ///     0 - Normalny użytkownik,
        ///     1 - Administrator rejestracji,
        ///     2 - Administrator</param>
        /// <param name="enabled">Czy użytkownik jest aktywny</param>
        /// <param name="cardNumber">Numer karty RFID (10 cyfr)</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public bool SSR_SetUserInfo(int dwMachineNumber, string enrollNumber, string name, 
            string password, int privilege, bool enabled, string cardNumber)
        {
            try
            {
                // 1. Jeśli podano numer karty, ustaw go najpierw
                if (!string.IsNullOrEmpty(cardNumber))
                {
                    if (!SetStrCardNumber(cardNumber))
                    {
                        return false; // Jeśli nie udało się ustawić karty, zwróć false
                    }
                }

                // 2. Ustaw podstawowe informacje o użytkowniku
                return _sdk.SSR_SetUserInfo(dwMachineNumber, enrollNumber, name, password, privilege, enabled);
            }
            catch
            {
                return false; // W przypadku jakiegokolwiek błędu zwróć false
            }
        }

        /// <summary>
        /// Usuwa dane użytkownika z urządzenia (nowa wersja API)
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <param name="enrollNumber">Numer ID użytkownika</param>
        /// <param name="backupNumber">Numer kopii zapasowej:
        ///     0 - Hasło
        ///     1-9 - Szablony odcisków palców
        ///     10 - Karta
        ///     11 - Wszystkie dane</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public bool SSR_DeleteEnrollData(int dwMachineNumber, string enrollNumber, int backupNumber)
        {
            try
            {
                int backupNumberRef = backupNumber;
                bool result = _sdk.SSR_DeleteEnrollData(dwMachineNumber, enrollNumber,  backupNumberRef);
                return result;
            }
            catch
            {
                return false;
            }
        }

        // Pobieranie zdarzeń (logów)
        public bool GetGeneralLogData(int dwMachineNumber, ref int dwTMachineNumber, ref int dwEnrollNumber, 
            ref int dwEMachineNumber, ref int dwVerifyMode, ref int dwInOutMode, ref int dwYear, ref int dwMonth, 
            ref int dwDay, ref int dwHour, ref int dwMinute)
            => _sdk.GetGeneralLogData(dwMachineNumber, ref dwTMachineNumber, ref dwEnrollNumber, ref dwEMachineNumber,
                ref dwVerifyMode, ref dwInOutMode, ref dwYear, ref dwMonth, ref dwDay, ref dwHour, ref dwMinute);
                
        public bool ReadGeneralLogData(int dwMachineNumber)
            => _sdk.ReadGeneralLogData(dwMachineNumber);
            
        public bool ReadTimeGLogData(int dwMachineNumber, string sTime, string eTime)
            => _sdk.ReadTimeGLogData(dwMachineNumber, sTime, eTime);

        // Zarządzanie danymi
        public bool ReadAllUserID(int dwMachineNumber)
            => _sdk.ReadAllUserID(dwMachineNumber);
            
        public bool RefreshData(int dwMachineNumber)
            => _sdk.RefreshData(dwMachineNumber);
            
        public bool ClearGLog(int dwMachineNumber)
            => _sdk.ClearGLog(dwMachineNumber);

        // Operacje na urządzeniu
        public bool EnableDevice(int dwMachineNumber, bool enabled)
            => _sdk.EnableDevice(dwMachineNumber, enabled);
            
        public bool RestartDevice(int dwMachineNumber)
            => _sdk.RestartDevice(dwMachineNumber);

        /// <summary>
        /// Pobiera pojedynczy rekord z logów urządzenia (nowa wersja API)
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <param name="enrollNumber">Numer ID użytkownika</param>
        /// <param name="verifyMode">Tryb weryfikacji:
        ///     0 - Hasło, 
        ///     1 - Odcisk palca, 
        ///     2 - Karta, 
        ///     3 - Tylko ID,
        ///     4 - Hasło + Odcisk,
        ///     5 - Odcisk + Karta,
        ///     6 - Hasło + Karta,
        ///     7 - Odcisk + Hasło + Karta,
        ///     8 - ID + Odcisk,
        ///     9 - Odcisk + Karta,
        ///     10 - ID + Karta</param>
        /// <param name="inOutMode">Tryb wejścia/wyjścia:
        ///     0 - Wejście,
        ///     1 - Wyjście,
        ///     2 - Przerwa,
        ///     3 - Powrót z przerwy,
        ///     4 - Nadgodziny,
        ///     5 - Koniec nadgodzin</param>
        /// <param name="year">Rok</param>
        /// <param name="month">Miesiąc (1-12)</param>
        /// <param name="day">Dzień (1-31)</param>
        /// <param name="hour">Godzina (0-23)</param>
        /// <param name="minute">Minuta (0-59)</param>
        /// <param name="second">Sekunda (0-59)</param>
        /// <param name="workCode">Kod pracy (jeśli używany)</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public bool SSR_GetGeneralLogData(int dwMachineNumber, out string enrollNumber, out int verifyMode, 
            out int inOutMode, out int year, out int month, out int day, out int hour, out int minute, 
            out int second, out int workCode)
        {
            // Inicjalizacja zmiennych
            enrollNumber = string.Empty;
            verifyMode = 0;
            inOutMode = 0;
            year = 0;
            month = 0;
            day = 0;
            hour = 0;
            minute = 0;
            second = 0;
            workCode = 0;

            try
            {
                return _sdk.SSR_GetGeneralLogData(dwMachineNumber, out enrollNumber, out verifyMode, out inOutMode,
                    out year, out month, out day, out hour, out minute, out second, ref workCode);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pobiera rozszerzone informacje o użytkowniku (nowa wersja API)
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <param name="enrollNumber">Numer ID użytkownika</param>
        /// <param name="name">Nazwa użytkownika</param>
        /// <param name="password">Hasło użytkownika</param>
        /// <param name="privilege">Uprawnienia:
        ///     0 - Normalny użytkownik,
        ///     1 - Administrator rejestracji,
        ///     2 - Administrator</param>
        /// <param name="enabled">Czy użytkownik jest aktywny</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public bool SSR_GetUserInfo(int dwMachineNumber, string enrollNumber, out string name, 
            out string password, out int privilege, out bool enabled)
            => _sdk.SSR_GetUserInfo(dwMachineNumber, enrollNumber, out name, out password, 
                out privilege, out enabled);

        /// <summary>
        /// Pobiera rozszerzone informacje o użytkowniku wraz z numerem karty
        /// </summary>
        public async Task<(bool success, string name, string password, int privilege, bool enabled, string cardNumber)> 
            GetUserInfoWithCard(int dwMachineNumber, string enrollNumber)
        {
            string name, password, cardNumber;
            int privilege;
            bool enabled;

            // Pobierz podstawowe informacje
            var success = SSR_GetUserInfo(dwMachineNumber, enrollNumber, out name, out password, out privilege, out enabled);
            
            // Pobierz numer karty
            if (success)
            {
                success = GetStrCardNumber(out cardNumber);
            }
            else
            {
                cardNumber = string.Empty;
            }

            return (success, name, password, privilege, enabled, cardNumber);
        }

        /// <summary>
        /// Usuwa wszystkie dane użytkownika
        /// </summary>
        /// <param name="dwMachineNumber">Numer urządzenia (zwykle 1)</param>
        /// <param name="enrollNumber">Numer ID użytkownika</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        public bool DeleteUser(int dwMachineNumber, string enrollNumber)
        {
            try
            {
                // Tworzymy zmienną lokalną i inicjalizujemy ją wartością stałej
                int backupNumber = BACKUP_NUMBER_ALL;
                // Przekazujemy zmienną lokalną przez ref
                return SSR_DeleteEnrollData(dwMachineNumber, enrollNumber, backupNumber);
            }
            catch
            {
                return false;
            }
        }

        public bool SSR_GetAllUserInfo(int dwMachineNumber, out string enrollNumber, out string name, 
            out string password, out int privilege, out bool enabled)
        {
            try
            {
                return _sdk.SSR_GetAllUserInfo(dwMachineNumber, out enrollNumber, out name, 
                    out password, out privilege, out enabled);
            }
            catch
            {
                enrollNumber = string.Empty;
                name = string.Empty;
                password = string.Empty;
                privilege = 0;
                enabled = false;
                return false;
            }
        }

        public bool GetFirmwareVersion(int dwMachineNumber, ref string strVersion)
        {
            try
            {
                return _sdk.GetFirmwareVersion(dwMachineNumber, ref strVersion);
            }
            catch
            {
                return false;
            }
        }

        public bool GetDeviceMAC(int dwMachineNumber, ref string strMAC)
        {
            try
            {
                return _sdk.GetDeviceMAC(dwMachineNumber, ref strMAC);
            }
            catch
            {
                return false;
            }
        }

        public bool GetPlatform(int dwMachineNumber, ref string strPlatform)
        {
            try
            {
                return _sdk.GetPlatform(dwMachineNumber, ref strPlatform);
            }
            catch
            {
                return false;
            }
        }

        public bool GetSerialNumber(int dwMachineNumber, ref string strSerialNumber)
        {
            try
            {
                return _sdk.GetSerialNumber(dwMachineNumber, out strSerialNumber);
            }
            catch
            {
                return false;
            }
        }

        public bool GetDeviceStrInfo(int dwMachineNumber, int dwInfo, out string stringValue)
        {
            try
            {
                return _sdk.GetDeviceStrInfo(dwMachineNumber, dwInfo, out stringValue);
            }
            catch
            {
                stringValue = string.Empty;
                return false;
            }
        }

        public bool GetDeviceInfo(int dwMachineNumber, int dwInfo, ref int dwValue)
        {
            try
            {
                return _sdk.GetDeviceInfo(dwMachineNumber, dwInfo, ref dwValue);
            }
            catch
            {
                return false;
            }
        }

        public bool GetDeviceStatus(int dwMachineNumber, int dwStatus, ref int dwValue)
        {
            try
            {
                return _sdk.GetDeviceStatus(dwMachineNumber, dwStatus, ref dwValue);
            }
            catch
            {
                return false;
            }
        }

        public bool GetSuperLogData(
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
            ref int dwSecond)
        {
            try
            {
                return _sdk.GetSuperLogData(
                    dwMachineNumber,
                    ref dwTMachineNumber,
                    ref dwSEnrollNumber,
                    ref dwParams4,
                    ref dwParams1,
                    ref dwParams2,
                    ref dwParams3,
                    ref dwYear,
                    ref dwMonth,
                    ref dwDay,
                    ref dwHour,
                    ref dwMinute,
                    ref dwSecond);
            }
            catch
            {
                return false;
            }
        }

        public bool ReadAllTemplate(int dwMachineNumber)
        {
            try
            {
                return _sdk.ReadAllTemplate(dwMachineNumber);
            }
            catch
            {
                return false;
            }
        }

        public bool GetUserTmpExStr(int dwMachineNumber, string dwEnrollNumber, int dwFingerIndex, out int flag, out string tmpData, out int tmpLength)
        {
            try
            {
                return _sdk.GetUserTmpExStr(dwMachineNumber, dwEnrollNumber, dwFingerIndex, out flag, out tmpData, out tmpLength);
            }
            catch
            {
                flag = 0;
                tmpData = string.Empty;
                tmpLength = 0;
                return false;
            }
        }

        public bool SetUserTmpExStr(int dwMachineNumber, string dwEnrollNumber, int dwFingerIndex, int flag, string tmpData)
        {
            try
            {
                return _sdk.SetUserTmpExStr(dwMachineNumber, dwEnrollNumber, dwFingerIndex, flag, tmpData);
            }
            catch
            {
                return false;
            }
        }
    }
} 