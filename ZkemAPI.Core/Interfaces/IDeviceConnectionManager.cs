using System;
using System.Threading;
using System.Threading.Tasks;
using ZkemAPI.Core.Models;

namespace ZkemAPI.Core.Interfaces
{
    /// <summary>
    /// Interfejs zarządzający połączeniami z czytnikami i synchronizacją dostępu
    /// </summary>
    public interface IDeviceConnectionManager
    {
        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu
        /// </summary>
        /// <typeparam name="T">Typ zwracanej wartości</typeparam>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        /// <param name="operation">Operacja do wykonania na czytniku</param>
        /// <returns>Wynik operacji</returns>
        Task<T> ExecuteDeviceOperationAsync<T>(string ipAddress, int port, Func<IZkemDevice, T> operation);

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu (bez zwrócenia wartości)
        /// </summary>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        /// <param name="operation">Operacja do wykonania na czytniku</param>
        Task ExecuteDeviceOperationAsync(string ipAddress, int port, Action<IZkemDevice> operation);

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu i timeout'em
        /// </summary>
        /// <typeparam name="T">Typ zwracanej wartości</typeparam>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        /// <param name="operation">Operacja do wykonania na czytniku</param>
        /// <param name="cancellationToken">Token anulowania operacji</param>
        /// <returns>Wynik operacji</returns>
        Task<T> ExecuteDeviceOperationAsync<T>(string ipAddress, int port, Func<IZkemDevice, T> operation, CancellationToken cancellationToken);

        /// <summary>
        /// Wykonuje operację na czytniku z synchronizacją dostępu i timeout'em (bez zwrócenia wartości)
        /// </summary>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        /// <param name="operation">Operacja do wykonania na czytniku</param>
        /// <param name="cancellationToken">Token anulowania operacji</param>
        Task ExecuteDeviceOperationAsync(string ipAddress, int port, Action<IZkemDevice> operation, CancellationToken cancellationToken);

        /// <summary>
        /// Sprawdza ile operacji czeka w kolejce dla danego czytnika
        /// </summary>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        /// <returns>Liczba oczekujących operacji</returns>
        int GetPendingOperationsCount(string ipAddress, int port);



        /// <summary>
        /// Zwalnia zasoby dla określonego czytnika
        /// </summary>
        /// <param name="ipAddress">Adres IP czytnika</param>
        /// <param name="port">Port czytnika</param>
        void ReleaseDevice(string ipAddress, int port);

        /// <summary>
        /// Zwalnia wszystkie zasoby
        /// </summary>
        void Dispose();
    }
} 