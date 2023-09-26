using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO.Ports;
using System.Collections.Concurrent; 


namespace VirtualRubik
{ 
    public class accelerometer
    {
        int x=0, y=0, z=0;
        int maxQueueLength = 512;
        ConcurrentQueue<Int32> dataQueue = new ConcurrentQueue<Int32>();
        ConcurrentQueue<Int32> smoothedx = new ConcurrentQueue<Int32>();
        ConcurrentQueue<Int32> smoothedy = new ConcurrentQueue<Int32>();
        ConcurrentQueue<Int32> smoothedz = new ConcurrentQueue<Int32>();
        int smoothingWindow = 8;

        public System.IO.Ports.SerialPort _serial;
        int byteState = 0;         // state variable. 1 = next byte is xaccel, 2 = next byte is yaccel, 3 = next byte is zaccel, 0 = waiting state
        public accelerometer(string port)
        {
            _serial = new SerialPort(port);
            _serial.BaudRate = 9600;
            _serial.Parity = Parity.None;
            _serial.StopBits = StopBits.One;
            _serial.DataBits = 8;
            _serial.Handshake = Handshake.None;
            _serial.RtsEnable = true;
            _serial.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            open();
        }
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            Int32 bytesToRead;
            Int32 newByte;

            while (_serial.BytesToRead != 0 && _serial != null)
            {
                bytesToRead = _serial.BytesToRead;
                newByte = _serial.ReadByte();
                dataQueue.Enqueue(newByte);                                             // new, use a queue
                
                if(dataQueue.Count > maxQueueLength)
                {
                    dataQueue.TryDequeue(out _);
                }
            }
        }
        public double getX()
        {
            if (smoothedx.Count > 0) return smoothedx.Average();
            else return 127;
        }
        public double getY()
        {
            if (smoothedy.Count > 0) return smoothedy.Average();
            else return 127;
        }
        public double getZ()
        {
            if (smoothedz.Count > 0) return smoothedz.Average();
            else return 127;
        }

        // Relies on a timer in the main application to call this as often as possible. Overflows at 512 values in queue.
        public void processByte()
        {
            int deQueueOut;
            foreach (Int32 reading in dataQueue)
            {
                dataQueue.TryDequeue(out deQueueOut);                               // peel off a data point and the timestamp associated therewith
                                                                                    //timeQueue.TryDequeue(out timestampOut);
                if (deQueueOut == 255)                                              // state check
                {
                    byteState = 0;
                }
                switch (byteState)
                {
                    case 0:
                        byteState = 1;
                        break;

                    case 1:
                        x = deQueueOut;
                        smoothedx.Enqueue(x);
                        byteState = 2;
                        break;

                    case 2:
                        y = deQueueOut;
                        smoothedy.Enqueue(y);
                        byteState = 3;
                        break;

                    case 3:
                        z = deQueueOut;
                        smoothedz.Enqueue(z);
                        byteState = 0;
                        break;

                    default:
                        byteState = 0;
                        break;
                }

                if(smoothedx.Count()>smoothingWindow)
                {
                    smoothedx.TryDequeue(out _);
                }
                if (smoothedy.Count() > smoothingWindow)
                {
                    smoothedy.TryDequeue(out _);
                }
                if (smoothedz.Count() > smoothingWindow)
                {
                    smoothedz.TryDequeue(out _);
                }
            }
        }

        public void open()
        {
            if (!_serial.IsOpen)
            {
                this._serial.Open();
            }
        }

        public void close()
        {
            if(_serial.IsOpen)
            {
                this._serial.Close();
                this._serial.Dispose();
            }
            
        }
    }

}
