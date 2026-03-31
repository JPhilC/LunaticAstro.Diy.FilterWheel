using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel
{
    public partial class SetupViewModel : ObservableObject
    {
        private Guid _uniqueId;  // The UniqueID of the driver instance, passed in from the Setup dialog constructor

        public event Action<bool>? CloseRequested;

        [ObservableProperty]
        private ObservableCollection<string> _comPorts = new();

        [ObservableProperty]
        private string? selectedComPort;

        [ObservableProperty]
        private bool traceEnabled;

        [ObservableProperty]
        private ObservableCollection<FilterEntryViewModel> filters = new();

        [ObservableProperty]
        private int selectedTabIndex;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string busyMessage;

        public SetupViewModel(Guid uniqueId)
        {
            // System.Diagnostics.Debugger.Launch();
            _uniqueId = uniqueId;

            if (FilterWheelHardware.IsInDesignMode)
                return; // Skip all ASCOM logic in the designer

            LoadComPorts();
            LoadSettings();
        }

        private void LoadComPorts()
        {
            ComPorts.Clear();
            foreach (var port in SerialPort.GetPortNames())
                ComPorts.Add(port);
        }

        public void LoadSettings()
        {
            // Load from ASCOM profile into static fields
            FilterWheelHardware.ReadProfile();

            // Copy static fields into ViewModel
            SelectedComPort = FilterWheelHardware.ComPort;
            TraceEnabled = FilterWheelHardware.Tl.Enabled;

            // Fallback if no saved COM port
            if (string.IsNullOrWhiteSpace(SelectedComPort) && ComPorts.Count > 0)
                SelectedComPort = ComPorts[0];

        }

        [RelayCommand]
        private void Ok()
        {
            SaveSettings();
            CloseRequested?.Invoke(true);
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(false);
        }


        private IAsyncRelayCommand? _saveFilterSettingsCommand;
        public IAsyncRelayCommand SaveFilterSettingsCommand =>
            _saveFilterSettingsCommand ??= new AsyncRelayCommand(SaveFilterSettingsAsync);

        private async Task SaveFilterSettingsAsync()
        {
            var names = Filters.Select(f => f.Name).ToArray();
            var positionOffsets = Filters.Select(f => f.PositionOffset).ToArray();
            var focusOffsets = Filters.Select(f => f.FocusOffset).ToArray();

            await FilterWheelHardware.SetFilterNamesAsync(names);
            await FilterWheelHardware.SetOffsetsAsync(positionOffsets);

            FilterWheelHardware.WriteFocusOffsetsToProfile(focusOffsets);
        }


        private IAsyncRelayCommand? _cancelFilterSettingsCommand;
        public IAsyncRelayCommand CancelFilterSettingsCommand =>
            _cancelFilterSettingsCommand ??= new AsyncRelayCommand(CancelFilterSettingsAsync);

        private async Task CancelFilterSettingsAsync()
        {
            await LoadFilterWheelDataAsync();
        }

        private void SaveSettings()
        {
            // Copy ViewModel values back into static fields
            FilterWheelHardware.ComPort = SelectedComPort ?? string.Empty;
            FilterWheelHardware.Tl.Enabled = TraceEnabled;

            // Persist to ASCOM Profile
            FilterWheelHardware.WriteProfile();
        }

        private async Task LoadFilterWheelDataAsync()
        {
            try
            {
                BusyMessage = "Connecting to filter wheel and loading settings...";
                IsBusy = true;
                await FilterWheelHardware.ConnectAsync(_uniqueId);
                BusyMessage = "Retrieving filter settings from hardware...";
                var names = await FilterWheelHardware.GetFilterNamesAsync();
                BusyMessage = "Retrieving filter position offsets from hardware...";
                var positionOffsets = await FilterWheelHardware.GetOffsetsAsync();
                BusyMessage = "Retrieving filter focus offsets from profile...";
                var focusOffsets = FilterWheelHardware.ReadFocusOffsetsFromProfile();
                BusyMessage = "Populating filter settings in UI...";
                Filters.Clear();

                for (int i = 0; i < names.Length; i++)
                {
                    FilterWheelHardware.LogMessage("SetupViewModel", $"Loading filter {i + 1}: Name='{names[i]}', PositionOffset={positionOffsets[i]}, FocusOffset={focusOffsets[i]}");
                    Filters.Add(new FilterEntryViewModel(
                        index: i + 1,
                        name: names[i],
                        positionOffset: positionOffsets[i],
                        focusOffset: focusOffsets[i]
                    ));
                }
            }
            finally
            {
                IsBusy = false;
                BusyMessage = "";
            }
        }


        #region Partial methods ....
        async partial void OnSelectedTabIndexChanged(int value)
        {
            if (value == 1)   // Tab 2 (0 = COM port tab)
            {
                await LoadFilterWheelDataAsync();
            }
        }

        #endregion
    }
}