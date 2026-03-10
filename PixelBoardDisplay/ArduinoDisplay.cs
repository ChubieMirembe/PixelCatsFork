using System;
using System.Collections.Generic;
using System.Timers;

namespace PixelBoard
{
    public class ArduinoDisplay : IDisplay
    {
        private readonly DisplayHelper dh = new DisplayHelper();
        private bool finishedStreaming = true;
        private readonly SerialPortManager serialPortManager = new SerialPortManager();

        private const int OutputLedCount = 256;
        private static readonly byte[] FrameMagic = new byte[] { (byte)'P', (byte)'B', (byte)'F', (byte)'R' };

        public ArduinoDisplay()
        {
            // Leave this disabled until host display is confirmed working.
            // new ArduinoInput(serialPortManager);

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

                serialPortManager.SerialPort.Write(header, 0, header.Length);
                serialPortManager.SerialPort.Write(rgb, 0, rgb.Length);
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
            this.dh.DisplayInt(value);
        }

        public void DisplayInt(int value, bool? leftAligned)
        {
            this.dh.DisplayInt(value, leftAligned);
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
    }
}