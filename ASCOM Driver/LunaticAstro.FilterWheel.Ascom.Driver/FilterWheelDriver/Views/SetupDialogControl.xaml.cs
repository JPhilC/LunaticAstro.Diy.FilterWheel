using ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.Views
{
    public partial class SetupDialogControl : UserControl
    {
        public event Action<bool>? CloseRequested;

        private readonly DispatcherTimer _spinTimer = new DispatcherTimer();

        public SetupDialogControl(SetupViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Configure timer
            _spinTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _spinTimer.Tick += SpinTimer_Tick;

            // Watch for IsBusy changes
            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Loaded += SetupDialogControl_Loaded;
        }

        private void SetupDialogControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModel.SetupViewModel vm)
            {
                vm.CloseRequested += result =>
                {
                    CloseRequested?.Invoke(result);
                };
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is SetupViewModel vm && e.PropertyName == nameof(SetupViewModel.IsBusy))
            {
                if (vm.IsBusy)
                    _spinTimer.Start();
                else
                    _spinTimer.Stop();
            }
        }

        private void SpinTimer_Tick(object? sender, EventArgs e)
        {
            if (Spinner.RenderTransform is RotateTransform rt)
            {
                rt.Angle = (rt.Angle + 6) % 360;
            }
        }

        private async void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Row.Item is FilterEntryViewModel vm)
                await vm.CommitEditsAsync();
        }

        private void BrowseToAscom(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://ascom-standards.org");
        }
    }

}