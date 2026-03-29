using DevFlowMonitor.Wpf.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DevFlowMonitor.Wpf.ViewModel
{
    public class StatusCardViewModel : INotifyPropertyChanged
    {
        
        public string Title { get; set; }
        public StatusCardType Type { get; init; }

        private int _value;
        public int Value
        {
            get => _value;
            set
            {
                if (value == _value) return;

                _value = value;
                OnPropertyChanged();
            }
        }

        #region OnPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
