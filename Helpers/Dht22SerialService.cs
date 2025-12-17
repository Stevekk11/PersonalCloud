using System;
using System.IO.Ports;
using System.Threading;
using Serilog;

namespace PersonalCloud.Helpers
{
    public class Dht22SerialService : IDisposable
    {
        private readonly SerialPort _serialPort;
        private double? _lastTemperature;
        private double? _lastHumidity;
        private readonly Thread _readThread;
        private bool _keepReading = true;
        private string? _lastError;

        public Dht22SerialService(string portName = "COM1", int baudRate = 9600)
        {
            try
            {
                _serialPort = new SerialPort(portName, baudRate)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000
                };
                _serialPort.Open();
                _readThread = new Thread(ReadLoop) { IsBackground = true };
                _readThread.Start();
            }
            catch (Exception e)
            {
                _lastError = e.Message;
                Log.Error(e, "Error while opening serial port.");
            }
        }

        private void ReadLoop()
        {
            while (_keepReading)
            {
                try
                {
                    var line = _serialPort.ReadLine();
                    ParseSensorData(line);
                    _lastError = null;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    Thread.Sleep(500);
                }
            }
        }

        private void ParseSensorData(string line)
        {
            // Expected format: "T:23.4;H:45.6"
            var parts = line.Split(';');
            foreach (var part in parts)
            {
                if (part.StartsWith("T:"))
                {
                    if (double.TryParse(part.Substring(2), out var temp))
                        _lastTemperature = temp;
                }
                else if (part.StartsWith("H:"))
                {
                    if (double.TryParse(part.Substring(2), out var hum))
                        _lastHumidity = hum;
                }
            }
        }

        public double? GetTemperature() => _lastTemperature;
        public double? GetHumidity() => _lastHumidity;
        public string? LastError => _lastError;
        public string PortName => _serialPort.PortName;
        public bool IsOpen => _serialPort.IsOpen;

        public void ChangePort(string portName, int baudRate = 9600)
        {
            try
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.Open();
                _lastError = null;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Error(ex, "Error while changing serial port.");
            }
        }

        public void Dispose()
        {
            _keepReading = false;
            _readThread.Join(1000);
            if (_serialPort.IsOpen) _serialPort.Close();
            _serialPort.Dispose();
        }
    }
}