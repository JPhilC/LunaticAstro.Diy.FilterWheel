using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ASCOM.LunaticAstro.FilterWheel.FilterWheelDriver.ViewModel
{
    public partial class FilterEntryViewModel : ObservableObject
    {
        private bool _nameDirty;
        private bool _positionDirty;
        private bool _focusDirty;


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

        partial void OnNameChanged(string value)
        {
            _nameDirty = true;
        }

        partial void OnPositionOffsetChanged(int value)
        {
            _positionDirty = true;
        }

        partial void OnFocusOffsetChanged(int value)
        {
            _focusDirty = true;
        }

        public async Task CommitEditsAsync()
        {
            if (_nameDirty)
            {
                await FilterWheelHardware.SetFilterNameAsync(Index, Name);
                _nameDirty = false;
            }

            if (_positionDirty)
            {
                await FilterWheelHardware.SetPositionOffsetAsync(Index, PositionOffset);
                _positionDirty = false;
            }

            if (_focusDirty)
            {
                FilterWheelHardware.WriteFocusOffsetToProfile(Index, FocusOffset);
                _focusDirty = false;
            }
        }


        [RelayCommand]
        private async Task IncrementOffset()
        {
            PositionOffset += 5;
            await CommitEditsAsync();
        }

        [RelayCommand]
        private async Task DecrementOffset()
        {
            PositionOffset -= 5;
            await CommitEditsAsync();
        }
    }
}