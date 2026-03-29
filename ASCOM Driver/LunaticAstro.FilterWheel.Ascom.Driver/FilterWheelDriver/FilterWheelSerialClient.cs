using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver
{
    internal class FilterWheelSerialClient : IDisposable
    {
        private readonly SerialPort _port;

        public bool IsOpen => _port.IsOpen;

        public Action<string>? Log { get; set; }

        public FilterWheelSerialClient(string portName, int baudRate = 115200)
        {
            _port = new SerialPort(portName, baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 2000,
                WriteTimeout = 2000,
                Handshake = Handshake.None,
                DtrEnable = true,
                RtsEnable = true,
                Encoding = System.Text.Encoding.ASCII
            };
        }

        
        public void Open()
        {
            if (!_port.IsOpen)
            {
                _port.Open();

                // Reset TCS for this connection
                _bootTcs = new TaskCompletionSource<bool>();
                _readyTcs = new TaskCompletionSource<string>();

                // Reset and restart background reader
                _readerCts?.Cancel();
                _readerCts = null;
                StartBackgroundReader();

                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();

                Thread.Sleep(1500);

                // Wait for OK
                if (!Task.WhenAny(_bootTcs.Task, Task.Delay(5000)).Result.Equals(_bootTcs.Task))
                    throw new Exception("No OK received from firmware.");
            }
        }

        public void Close()
        {
            if (_port.IsOpen)
            {
                _readerCts?.Cancel();
                _readerCts = null;
                _port.Close();
            }
        }

        // Core synchronous implementation
        public void SendCommand(string command)
        {
            if (!_port.IsOpen)
                throw new InvalidOperationException("Serial port is not open.");

            Log?.Invoke($"TX: {command}");
            _port.WriteLine(command);
        }

        // Async wrapper for existing service code
        public Task SendCommandAsync(string command)
            => Task.Run(() => SendCommand(command));

        public async Task<string> ReadResponseAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (_responseQueue.TryDequeue(out var line))
                    return line;

                await Task.Run(() => _responseEvent.WaitOne(100), token);
            }
        }

        public Task<string> WaitForReadyAsync()
        {
            if (_readyTcs == null)
                throw new InvalidOperationException("Client not opened.");

            return _readyTcs.Task;
        }

        public async Task<string> WaitForResponseAsync(
                Func<string, bool> predicate,
                CancellationToken cancellationToken = default)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await ReadResponseAsync(cancellationToken);

                if (predicate(line))
                    return line;
            }
        }

        private CancellationTokenSource? _readerCts;

        public void StartBackgroundReader()
        {
            if (_readerCts != null)
                return;

            _readerCts = new CancellationTokenSource();
            var token = _readerCts.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && _port.IsOpen)
                {
                    try
                    {
                        string line = _port.ReadLine().Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            Log?.Invoke($"RX: {line} (background)");
                            ProcessIncomingLine(line);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // normal
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"Background reader error: {ex.Message}");
                    }
                }
            }, token);
        }

        private TaskCompletionSource<bool>? _bootTcs;
        private TaskCompletionSource<string>? _readyTcs;
        private readonly ConcurrentQueue<string> _responseQueue = new();
        private readonly AutoResetEvent _responseEvent = new(false);

        private void ProcessIncomingLine(string line)
        {
            if (line.StartsWith("CONNECTED", StringComparison.OrdinalIgnoreCase))
            {
                _bootTcs?.TrySetResult(true);
                return;
            }

            if (line.StartsWith("READY", StringComparison.OrdinalIgnoreCase))
            {
                _readyTcs?.TrySetResult(line);
                return;
            }

            // Otherwise this is a command response
            _responseQueue.Enqueue(line);
            _responseEvent.Set();

        }

        public void Dispose()
        {
            if (_port.IsOpen)
                _port.Close();

            _port.Dispose();
        }
    }
}