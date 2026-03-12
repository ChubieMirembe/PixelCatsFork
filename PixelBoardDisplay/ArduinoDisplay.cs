using System;
using System.Collections.Generic;
using System.Timers;
using System.Text;

namespace PixelBoard
{
    public class ArduinoDisplay : IDisplay
    {
        private readonly DisplayHelper dh = new DisplayHelper();
        private bool finishedStreaming = true;
        private readonly SerialPortManager serialPortManager = new SerialPortManager();

        // Add a lock so PBFR (large frame) and PBLC (small LCD) writes are atomic w.r.t each other.
        private readonly object serialLock = new object();

        private const int OutputLedCount = 256;
        private static readonly byte[] FrameMagic = new byte[] { (byte)'P', (byte)'B', (byte)'F', (byte)'R' };

        public ArduinoDisplay()
        {
            // Leave this disabled until host display is confirmed working.
            new ArduinoInput(serialPortManager);

            this.dh.SetSize(20, 10);
            this.dh.SetFramerate(15);

            initBoard();

            ElapsedEventHandler dtfr = drawToFramerate;
            this.dh.MakeTimer(dtfr);
        }

        public ArduinoDisplay(sbyte height, sbyte width, sbyte framerate = 15)
        {
            this.dh.SetFramerate(framerate);
            this.dh.SetSize(height, width);

            initBoard();

            ElapsedEventHandler dtfr = drawToFramerate;
            this.dh.MakeTimer(dtfr);
        }

        public void DrawBatch(IEnumerable<ILocatedPixel> pixels)
        {
            foreach (var pixel in pixels)
            {
                this.dh.Draw(pixel);
            }
        }

        private void initBoard()
        {
            this.dh.currentBoard = new Pixel[this.dh.height, this.dh.width];
            for (sbyte i = 0; i < this.dh.height; i++)
            {
                for (sbyte j = 0; j < this.dh.width; j++)
                {
                    dh.currentBoard[i, j] = new Pixel(0, 0, 0);
                }
            }
        }

        private void drawToFramerate(object source, ElapsedEventArgs e)
        {
            if (!finishedStreaming)
            {
                return;
            }

            if (!serialPortManager.SerialPort.IsOpen)
            {
                return;
            }

            finishedStreaming = false;

            try
            {
                this.dh.RefreshDisplay(this);

                Pixel[,] toDraw = new Pixel[this.dh.height, this.dh.width];
                Array.Copy(this.dh.currentBoard, toDraw, this.dh.currentBoard.Length);

                byte[] rgb = new byte[OutputLedCount * 3];

                int counter = 0;
                for (sbyte j = 0; j < this.dh.width; j++)
                {
                    bool reverseColumn = (j % 2 == 1); // Serpentine: reverse odd columns (vertical serpentine)

                    for (sbyte i = 0; i < this.dh.height; i++)
                    {
                        if (counter + 2 >= rgb.Length)
                        {
                            break;
                        }

                        // No global vertical flip — row 0 is top in currentBoard.
                        // For odd columns, iterate bottom-to-top; for even columns, top-to-bottom.
                        sbyte row = reverseColumn ? (sbyte)(this.dh.height - 1 - i) : i;
                        Pixel p = toDraw[row, j];
                        if (p != null)
                        {
                            // Arduino expects GRB
                            rgb[counter + 0] = p.Green;
                            rgb[counter + 1] = p.Red;
                            rgb[counter + 2] = p.Blue;
                        }

                        counter += 3;
                    }
                }

                ushort payloadLength = (ushort)rgb.Length;

                byte[] header = new byte[6];
                header[0] = FrameMagic[0];
                header[1] = FrameMagic[1];
                header[2] = FrameMagic[2];
                header[3] = FrameMagic[3];
                header[4] = (byte)(payloadLength & 0xFF);
                header[5] = (byte)((payloadLength >> 8) & 0xFF);

                // Ensure the two writes (header + payload) are atomic with respect to LCD writes.
                lock (serialLock)
                {
                    serialPortManager.SerialPort.Write(header, 0, header.Length);
                    serialPortManager.SerialPort.Write(rgb, 0, rgb.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PixelBoard] Serial write failed: {ex.Message}");
            }
            finally
            {
                finishedStreaming = true;
            }
        }

        public void DisplayInt(int value)
        {
            // Update local state first
            this.dh.DisplayInt(value);

            // Send LCD packet to Arduino
            SendLcdTextToArduino(this.dh.currentLCDNumber);
        }

        public void DisplayInt(int value, bool? leftAligned)
        {
            // Update local state first
            this.dh.DisplayInt(value, leftAligned);

            // Send LCD packet to Arduino
            SendLcdTextToArduino(this.dh.currentLCDNumber);
        }

        public void DisplayInts(int leftValue, int rightValue)
        {
            this.dh.DisplayInts(leftValue, rightValue);
        }

        public void Draw(IPixel[,] pixels)
        {
            this.dh.Draw(pixels);
        }

        public void Draw(ILocatedPixel pixel)
        {
            this.dh.Draw(pixel);
        }
        public void DisplayText(string text)
        {
            // Update local state first
            this.dh.currentLCDNumber = text ?? "";

            // Send LCD packet to Arduino
            SendLcdTextToArduino(this.dh.currentLCDNumber);
        }
        private void SendLcdTextToArduino(string text)
        {
            if (string.IsNullOrEmpty(text)) text = "";

            var serial = serialPortManager.SerialPort;
            if (serial == null || !serial.IsOpen) return;

            try
            {
                // Packet: 4-byte magic 'P','B','L','C' + 2-byte length (little-endian) + payload bytes (UTF8)
                byte[] magic = new byte[] { (byte)'P', (byte)'B', (byte)'L', (byte)'C' };
                byte[] payload = Encoding.UTF8.GetBytes(text);
                ushort len = (ushort)payload.Length;
                byte[] header = new byte[6];
                header[0] = magic[0];
                header[1] = magic[1];
                header[2] = magic[2];
                header[3] = magic[3];
                header[4] = (byte)(len & 0xFF);
                header[5] = (byte)((len >> 8) & 0xFF);

                // Hold the same lock as frame writes to avoid interleaving
                lock (serialLock)
                {
                    serial.Write(header, 0, header.Length);
                    if (len > 0)
                        serial.Write(payload, 0, payload.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PixelBoard] Failed to send LCD packet: {ex.Message}");
            }
        }
        public void Display7SegHud(byte mask0, byte mask1, byte mask2,
                           (byte r, byte g, byte b) col0,
                           (byte r, byte g, byte b) col1,
                           (byte r, byte g, byte b) col2,
                           int score)
        {
            var serial = serialPortManager.SerialPort;
            if (serial == null || !serial.IsOpen) return;

            int last3 = Math.Abs(score) % 1000;
            byte d100 = (byte)((last3 / 100) % 10);
            byte d10 = (byte)((last3 / 10) % 10);
            byte d1 = (byte)((last3 / 1) % 10);

            byte[] payload = new byte[15];
            payload[0] = mask0;
            payload[1] = mask1;
            payload[2] = mask2;

            payload[3] = col0.r; payload[4] = col0.g; payload[5] = col0.b;
            payload[6] = col1.r; payload[7] = col1.g; payload[8] = col1.b;
            payload[9] = col2.r; payload[10] = col2.g; payload[11] = col2.b;

            payload[12] = d100;
            payload[13] = d10;
            payload[14] = d1;

            byte[] header = new byte[6];
            header[0] = (byte)'P';
            header[1] = (byte)'B';
            header[2] = (byte)'7';
            header[3] = (byte)'S';
            header[4] = (byte)(payload.Length & 0xFF);
            header[5] = (byte)((payload.Length >> 8) & 0xFF);

            lock (serialLock)
            {
                serial.Write(header, 0, header.Length);
                serial.Write(payload, 0, payload.Length);
            }
        }
    }
}