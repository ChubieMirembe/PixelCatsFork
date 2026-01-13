using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace PixelBoard
{
    public class SerialPortManager
    {
        private static SerialPort serialPort = new SerialPort();

        public SerialPort SerialPort { get => serialPort; }

        public SerialPortManager()
        {
            while (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.PortName = "COM3";
                    serialPort.BaudRate = 1000000;
                   // serialPort.WriteBufferSize = 64;
                    serialPort.Open();
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
                catch (System.IO.IOException e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
                Thread.Sleep(1);
            }
        }
    }
}
