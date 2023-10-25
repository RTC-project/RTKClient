using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTKClientLib
{
    public class Connection
    {
        private SerialPort _port;

        public Connection(string com = "COM20")
        {
            _port = new SerialPort(com, 57600);
            _port.DataReceived += _port_DataReceived;
            _port.Open();
            _port.NewLine = "\n";
        }

        private void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //
        }

        public void SendTestMessage()
        {
            _port.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 10}, 0, 9);
        }

        public void SendCommand(byte[] data)
        {
            _port.Write(data, 0, data.Length);
        }
    }
}
