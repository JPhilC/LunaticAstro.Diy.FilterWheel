using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Xml.Linq;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel
{
    public partial class FilterEntryViewModel : ObservableObject
    {
        public int Index { get; }   // 1-based for display

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private int positionOffset;

        [ObservableProperty]
        private int focusOffset;

        public FilterEntryViewModel(int index, string name, int positionOffset, int focusOffset = 0)
        {
            Index = index;
            this.name = name;
            this.positionOffset = positionOffset;
            this.focusOffset = focusOffset;
        }

        [RelayCommand]
        private void IncrementOffset() => positionOffset += 5;

        [RelayCommand]
        private void DecrementOffset() => positionOffset -= 5;
    }
}