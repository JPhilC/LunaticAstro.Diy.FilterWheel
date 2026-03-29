using ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.Views
{
    public partial class SetupDialogControl : UserControl
    {
        public event Action<bool>? CloseRequested;

        public SetupDialogControl(SetupViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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

        private void BrowseToAscom(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start("https://ascom-standards.org");
        }
    }

}