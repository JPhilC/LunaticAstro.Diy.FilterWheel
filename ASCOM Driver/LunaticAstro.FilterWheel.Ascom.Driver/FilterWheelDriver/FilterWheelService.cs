using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Markup;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver
{
    internal class FilterWheelService : IDisposable
    {
        private readonly FilterWheelSerialClient _client;

        public bool IsConnected => _client.IsOpen;

        public FilterWheelService(string comPort, int baudRate = 115200)
        {
            _client = new FilterWheelSerialClient(comPort, baudRate);
            // Attach logging
            _client.Log = msg => FilterWheelHardware.LogMessage("Serial", msg);
        }

        public void Connect()
        {
            if (!IsConnected)
            {
                _client.Open();
                _client.StartBackgroundReader();
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
                _client.Close();
        }

        public void Dispose()
        {
            Disconnect();
            _client.Dispose();
        }

        public async Task WaitForReadyAsync()
        {
            // Wait for P<n> from firmware
            string ready = await _client.WaitForReadyAsync();

            FilterWheelHardware.LogMessage("WaitForReadyAsync", $"Firmware ready: {ready}");
        }


        // ------------------------------------------------------------
        // 1. MOVEMENT COMMANDS
        // ------------------------------------------------------------

        public async Task<int> GoToPositionAsync(int position)
        {
            if (position < 0 || position > 8)
                throw new ArgumentOutOfRangeException(nameof(position));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync($"G{position}");

            // 2. Wait for EXACT P<n>
            var response = await _client.WaitForResponseAsync(
                line => line.Equals($"P{position}", StringComparison.OrdinalIgnoreCase),
                cts.Token);

            if (!response.StartsWith($"P{position}", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected response to G{position}: {response}");

            return ParsePosition(response);
        }

        public async Task SaveConfigurationAsync()
        {
            await _client.SendCommandAsync("G0");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var response = await _client.WaitForResponseAsync(
                line => line.StartsWith("EEPROM Saved", StringComparison.OrdinalIgnoreCase),
                cts.Token);

            if (!response.StartsWith("EEPROM Saved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected response to G0: '{response}'");
        }

        // ------------------------------------------------------------
        // 2. OFFSET COMMANDS
        // ------------------------------------------------------------

        public async Task<int[]> GetOffsetsAsync(int slotCount)
        {
            int[] offsets = new int[slotCount];

            for (int i = 0; i < slotCount; i++)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                int slot = i + 1;

                await _client.SendCommandAsync($"O{slot}");

                var response = await _client.WaitForResponseAsync(
                    line =>
                        line.StartsWith($"P{slot}", StringComparison.OrdinalIgnoreCase) &&
                        line.Contains("Offset", StringComparison.OrdinalIgnoreCase),
                    cts.Token);

                offsets[i] = ParsePositionOffset(response).offset;
            }

            return offsets;
        }

        public async Task<(int position, int offset)> GetOffsetForFilterAsync(int position)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            if (position < 1 || position > 8)
                throw new ArgumentOutOfRangeException(nameof(position));

            await _client.SendCommandAsync($"O{position}");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith($"P{position}", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Offset", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            return ParsePositionOffset(response);
        }


        public async Task<(int position, int offset)> SetOffsetAsync(int position, int value)
        {
            if (position < 1 || position > 8)
                throw new ArgumentOutOfRangeException(nameof(position));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // 1. Send F<n> <xxx>
            await _client.SendCommandAsync($"F{position} {value}");

            // 2. Wait for the final "P<n> Offset <xxx>" and ignore any plain P<n>
            var offsetResponse = await _client.WaitForResponseAsync(
                line =>
                    line.StartsWith($"P{position}", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("Offset", StringComparison.OrdinalIgnoreCase),
                cts.Token);

            var offsetParsed = ParsePositionOffset(offsetResponse);

            if (offsetParsed.position != position)
                throw new InvalidOperationException(
                    $"Firmware applied offset to wrong position. Expected {position}, got {offsetParsed.position}");

            if (offsetParsed.offset != value)
                throw new InvalidOperationException(
                    $"Firmware returned unexpected offset. Expected {value}, got {offsetParsed.offset}");

            return offsetParsed;
        }


        // ------------------------------------------------------------
        // 2a. NAME COMMANDS
        // ------------------------------------------------------------
        public async Task<string[]> GetNamesAsync(int slotCount)
        {
            var names = new string[slotCount];
            for (int i = 1; i <= slotCount; i++)
            {
                await _client.SendCommandAsync($"Q{i}");
                var response = await _client.WaitForResponseAsync(
                line =>
                    line.StartsWith($"P{i}", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("Name", StringComparison.OrdinalIgnoreCase));

                names[i - 1] = ParseName(response).name;
            }
            return names;
        }

        public async Task<(int position, string name)> GetNameForFilterAsync(int position)
        {
            if (position < 1 || position > 8)
                throw new ArgumentOutOfRangeException(nameof(position));

            await _client.SendCommandAsync($"Q{position}");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith($"P{position}", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Name", StringComparison.OrdinalIgnoreCase));

            return ParseName(response);
        }

        public async Task<(int position, string name)> SetNameAsync(int value, string name)
        {
            await _client.SendCommandAsync($"N{value} {name}");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith($"P{value}", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Name", StringComparison.OrdinalIgnoreCase));
            var result = ParseName(response);
            if (result.name != name)
                throw new InvalidOperationException(
                    $"Firmware returned unexpected name. Expected '{name}', got '{result.name}'");
            return result;
        }


        // ------------------------------------------------------------
        // 3. INFORMATION COMMANDS (I0–I9)
        // ------------------------------------------------------------

        public async Task<string> GetProductNameAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await _client.SendCommandAsync("I0");

            return await _client.WaitForResponseAsync(line => !string.IsNullOrEmpty(line), cts.Token);
        }

        public async Task<string> GetFirmwareVersionAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await _client.SendCommandAsync("I1");

            return await _client.WaitForResponseAsync(line => !string.IsNullOrEmpty(line), cts.Token);
        }

        public async Task<int> GetCurrentPositionAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            await _client.SendCommandAsync("I2");

            var response = await _client.WaitForResponseAsync(
                line => line.StartsWith("P", StringComparison.OrdinalIgnoreCase),
                cts.Token);

            return ParsePosition(response);
        }

        public async Task<string> GetSerialNumberAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("I3");
            return await _client.WaitForResponseAsync(line => !string.IsNullOrEmpty(line), cts.Token);
        }


        public async Task<int> GetMaximumSpeedAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("I4");
            // "MaxSpeed <maxSpeed>"
            var response = await _client.WaitForResponseAsync(
                line => line.StartsWith("MaxSpeed", StringComparison.OrdinalIgnoreCase),
                cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var speed))
                throw new InvalidOperationException($"Unexpected I4 response: '{response}'");
            return speed;
        }

        public async Task<(int position, int offset)> GetCurrentFilterOffsetAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("I5");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith($"P", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Offset", StringComparison.OrdinalIgnoreCase),
            cts.Token);

            return ParsePositionOffset(response);
        }

        public async Task<int> GetThresholdAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("I6");
            // "Threshold <analogSensorThreshold>"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Threshold", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var threshold))
                throw new InvalidOperationException($"Unexpected I6 response: '{response}'");
            return threshold;
        }

        public async Task<int> GetFilterSlotCountAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("I7");
            // "FilterSlots <numberOfFilters>"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("FilterSlots", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            FilterWheelHardware.LogMessage("GetFilterSlotCountAsync", $"Response: {response}");
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var count))
                throw new InvalidOperationException($"Unexpected I7 response: '{response}'");
            return count;
        }

        // ------------------------------------------------------------
        // 4. SENSOR TEST COMMANDS (T0–T3)
        // ------------------------------------------------------------

        public async Task<(bool sensor1, bool sensor2)> GetSensorDigitalStateAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("T0");
            // "Sensors <state> <state>"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Sensors", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                throw new InvalidOperationException($"Unexpected T0/T1 response: '{response}'");

            bool s1 = ParseBool(parts[1]);
            bool s2 = ParseBool(parts[2]);
            return (s1, s2);
        }

        public async Task<bool> IsSensorDigitalAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); 
            await _client.SendCommandAsync("T2");
            // "Digital YES" / "Digital NO"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Digital", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException($"Unexpected T2 response: '{response}'");
            return parts[1].Equals("YES", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> IsSensorActiveHighAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("T3");
            // "Active High YES" / "Active High NO"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Active High", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                throw new InvalidOperationException($"Unexpected T3 response: '{response}'");
            return parts[2].Equals("YES", StringComparison.OrdinalIgnoreCase);
        }

        // ------------------------------------------------------------
        // 5. RESET / CALIBRATION COMMANDS (R1–R6)
        // ------------------------------------------------------------

        public async Task ReInitialiseAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _client.SendCommandAsync("R1");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("P1", StringComparison.OrdinalIgnoreCase),
            cts.Token);

            if (!response.StartsWith($"P1", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected response to R1: {response}");

        }

        public async Task ResetAllOffsetsAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("R2");
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Calibration Removed", StringComparison.OrdinalIgnoreCase),
            cts.Token);

            if (!response.StartsWith("Calibration Removed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected R2 response: '{response}'");
        }

        public async Task<int> ResetSpeedTo100Async()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync("R3");
            // "MaxSpeed 100%"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("MaxSpeed", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException($"Unexpected R4 response: '{response}'");

            var percentStr = parts[1].TrimEnd('%');
            if (!int.TryParse(percentStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
                throw new InvalidOperationException($"Unexpected R4 percent: '{parts[1]}'");

            return percent;
        }

        public async Task<int> ReDetectSensorAndReInitialiseAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await _client.SendCommandAsync("R4");
            // "Threshold <analogSensorThreshold>"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("Threshold", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out var threshold))
                throw new InvalidOperationException($"Unexpected R4 response: '{response}'");
            return threshold;
        }

        // ------------------------------------------------------------
        // 6. SPEED COMMANDS (Sxxx)
        // ------------------------------------------------------------

        public async Task<int> SetRotationSpeedPercentAsync(int percent)
        {
            if (percent < 0 || percent > 100)
                throw new ArgumentOutOfRangeException(nameof(percent));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _client.SendCommandAsync($"S{percent}");
            // "MaxSpeed <percent>%"
            var response = await _client.WaitForResponseAsync(
            line =>
                line.StartsWith("MaxSpeed", StringComparison.OrdinalIgnoreCase),
            cts.Token);
            var parts = response.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException($"Unexpected S response: '{response}'");

            var percentStr = parts[1].TrimEnd('%');
            if (!int.TryParse(percentStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                throw new InvalidOperationException($"Unexpected S percent: '{parts[1]}'");

            return result;
        }

        // ------------------------------------------------------------
        // PARSING HELPERS
        // ------------------------------------------------------------

        private static int ParsePosition(string response)
        {
            // "P<n>"
            if (!response.StartsWith("P", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unexpected position response: '{response}'");

            if (!int.TryParse(response.Substring(1), out var pos))
                throw new InvalidOperationException($"Invalid position value in '{response}'");

            return pos;
        }

        private static (int position, int offset) ParsePositionOffset(string response)
        {
            // "P<n> Offset X"
            // Use regex for robustness
            var match = Regex.Match(response, @"^P(\d+)\s+Offset\s+(-?\d+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
                throw new InvalidOperationException($"Unexpected position/offset response: '{response}'");

            var pos = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var offset = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            return (pos, offset);
        }

        private static bool ParseBool(string value)
        {
            return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("YES", StringComparison.OrdinalIgnoreCase);
        }
        private static (int position, string name) ParseName(string response)
        {
            // Expected: "P<n> Name <name>"
            var match = Regex.Match(
                response,
                @"^P(\d+)\s+Name\s+(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );

            if (!match.Success)
                throw new InvalidOperationException(
                    $"Unexpected position/name response: '{response}'");

            var pos = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var name = match.Groups[2].Value.Trim();

            return (pos, name);
        }

    }
}