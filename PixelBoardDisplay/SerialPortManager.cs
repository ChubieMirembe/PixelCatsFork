using System;
using System.IO.Ports;
using System.Threading;

namespace PixelBoard
{
    public class SerialPortManager
    {
        private static SerialPort serialPort = new SerialPort();

        public SerialPort SerialPort { get => serialPort; }

        public SerialPortManager(string portName = "COM5", int baudRate = 115200)
        {
            // Configure once
            serialPort.PortName = portName;
            serialPort.BaudRate = baudRate;
            serialPort.Parity = Parity.None;
            serialPort.DataBits = 8;
            serialPort.StopBits = StopBits.One;
            serialPort.Handshake = Handshake.None;

            // Avoid accidental auto-reset on Arduino while debugging — toggle as needed
            serialPort.DtrEnable = false;
            serialPort.RtsEnable = false;

            // Prevent blocking ReadByte/Write calls from hanging indefinitely
            serialPort.ReadTimeout = 500;
            serialPort.WriteTimeout = 500;

            // Try to open with a modest retry delay
            while (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.Open();
                    Console.WriteLine($"SerialPort opened: {serialPort.PortName} @ {serialPort.BaudRate}");
                }
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine($"Access denied opening {portName}: {e.Message}");
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine($"Invalid operation opening {portName}: {e.Message}");
                }
                catch (System.IO.IOException e)
                {
                    Console.WriteLine($"I/O error opening {portName}: {e.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error opening {portName}: {e.Message}");
                }

                if (!serialPort.IsOpen)
                {
                    // Helpful diagnostic: show available ports once per retry
                    var ports = SerialPort.GetPortNames();
                    Console.WriteLine($"Available COM ports: {string.Join(", ", ports)}");
                    Thread.Sleep(250); // avoid tight busy loop
                }
            }
        }
    }
}
