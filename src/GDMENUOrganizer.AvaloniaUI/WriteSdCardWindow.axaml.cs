using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace GDMENUOrganizer
{
    public partial class WriteSdCardWindow : Window, INotifyPropertyChanged
    {
        public new event PropertyChangedEventHandler PropertyChanged;

        public string CardName { get; }

        public string PromptText => $"Write card \"{CardName}\" to an SD drive";

        public ObservableCollection<DriveInfo> DriveList { get; }

        private DriveInfo _selectedDrive;
        private string _selectedDriveVolumeLabel = string.Empty;
        private readonly Func<Task> _refreshDrives;

        public DriveInfo SelectedDrive
        {
            get => _selectedDrive;
            set
            {
                _selectedDrive = value;
                _selectedDriveVolumeLabel = string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDriveVolumeLabel));
                if (value != null)
                    _ = UpdateVolumeLabelAsync(value);
            }
        }

        public string SelectedDriveVolumeLabel => _selectedDriveVolumeLabel;

        public WriteSdCardWindow()
            : this(string.Empty, new ObservableCollection<DriveInfo>(), null)
        {
        }

        public WriteSdCardWindow(
            string cardName,
            ObservableCollection<DriveInfo> driveList,
            DriveInfo selectedDrive,
            Func<Task> refreshDrives = null
        )
        {
            CardName = cardName ?? string.Empty;
            DriveList = driveList;
            _refreshDrives = refreshDrives;
            InitializeComponent();
            DataContext = this;
            SelectedDrive = selectedDrive;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void ButtonRefreshDrive_Click(object sender, RoutedEventArgs e)
        {
            if (_refreshDrives != null)
                await _refreshDrives();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void ButtonWrite_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedDrive == null)
                return;
            Close(SelectedDrive);
        }

        private async Task UpdateVolumeLabelAsync(DriveInfo drive)
        {
            try
            {
                var label = await Task.Run(() =>
                {
                    try
                    {
                        return drive.VolumeLabel;
                    }
                    catch
                    {
                        return string.Empty;
                    }
                });

                if (ReferenceEquals(_selectedDrive, drive))
                {
                    _selectedDriveVolumeLabel = label ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedDriveVolumeLabel));
                }
            }
            catch
            {
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
