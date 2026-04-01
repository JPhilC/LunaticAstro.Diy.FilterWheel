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

        private List<FilterEntryViewModel> _originalFilters = new();


        [ObservableProperty]
        private int selectedTabIndex;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string busyMessage;

        [ObservableProperty]
        private FilterEntryViewModel? selectedFilter;


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
        private async Task Cancel()
        {
            await CancelFilterSettingsAsync();
            CloseRequested?.Invoke(false);
        }



        private async Task CancelFilterSettingsAsync()
        {
            IsBusy = true;
            BusyMessage = "Reverting changes…";

            try
            {
                // Restore UI list
                Filters = new ObservableCollection<FilterEntryViewModel>(
                    _originalFilters.Select(f =>
                        new FilterEntryViewModel(f.Index, f.Name, f.PositionOffset, f.FocusOffset))
                );

                // Push restored values back to hardware
                foreach (var f in _originalFilters)
                {
                    await FilterWheelHardware.SetFilterNameAsync(f.Index, f.Name);
                    await FilterWheelHardware.SetPositionOffsetAsync(f.Index, f.PositionOffset);
                    FilterWheelHardware.WriteFocusOffsetToProfile(f.Index, f.FocusOffset);
                }
            }
            finally
            {
                IsBusy = false;
                BusyMessage = "";
                CloseRequested?.Invoke(false);   // false = cancelled
            }
        }

        private void SaveSettings()
        {
            // Copy ViewModel values back into static fields
            FilterWheelHardware.ComPort = SelectedComPort ?? string.Empty;
            FilterWheelHardware.Tl.Enabled = TraceEnabled;

            // Persist to ASCOM Profile
            FilterWheelHardware.WriteProfile();
        }


        private bool _initialising;
        private async Task LoadFilterWheelDataAsync()
        {
            try
            {
                _initialising = true;
                BusyMessage = "Connecting to filter wheel and loading settings...";
                IsBusy = true;
                await FilterWheelHardware.ConnectAsync(_uniqueId);
                var names = await FilterWheelHardware.GetFilterNamesAsync();
                var positionOffsets = await FilterWheelHardware.GetPositionOffsetsAsync();
                var focusOffsets = FilterWheelHardware.ReadFocusOffsetsFromProfile();
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
                    _originalFilters.Add(new FilterEntryViewModel(
                        index: i + 1,
                        name: names[i],
                        positionOffset: positionOffsets[i],
                        focusOffset: focusOffsets[i]
                    ));
                }

                SelectedFilter = Filters.FirstOrDefault();
                _initialising = false;
            }
            finally
            {
                IsBusy = false;
                BusyMessage = "";
            }
        }


        private async Task MoveToFilterAsync(int firmwareIndex)
        {
            try
            {
                IsBusy = true;
                await FilterWheelHardware.GoToFilterAsync(firmwareIndex);
            }
            finally
            {
                IsBusy = false;
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

        partial void OnSelectedFilterChanged(FilterEntryViewModel? value)
        {
            if (_initialising || value is null)
                return;

            // Index is 1‑based for display, firmware expects 0‑based
            _ = MoveToFilterAsync(value.Index);
        }
        #endregion
    }
}