namespace ZkemAPI.SDK.Interfaces
{
    public interface IZkemDevice
    {
        bool Connect_Net(string IPAdd, int Port);
        void Disconnect();
        bool GetDeviceTime(int dwMachineNumber, ref int dwYear, ref int dwMonth, ref int dwDay, ref int dwHour, ref int dwMinute, ref int dwSecond);
        // ... dodaj inne potrzebne metody z interfejsu
    }
} 