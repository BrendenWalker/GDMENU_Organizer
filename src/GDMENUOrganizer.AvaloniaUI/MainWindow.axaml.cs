using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using GDMENUOrganizer.Core;
using GDMENUOrganizer.Core.Database;
using MsBox.Avalonia;
using MsBox.Avalonia.Models;
using NiceIO;

namespace GDMENUOrganizer.AvaloniaUI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private Manager Manager { get; }

        private readonly bool _showAllDrives;

        public new event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<DriveInfo> DriveList { get; } = new();

        private ObservableCollection<CardRecord> CardList { get; } = new();

        private ObservableCollection<GdItem> CardGames { get; } = new();

        private bool _isBusy;

        private bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                RaisePropertyChanged();
            }
        }

        private DriveInfo _driveInfo;
        private string _selectedDriveVolumeLabel = string.Empty;
        private CardRecord _selectedCard;
        private string _cardGamesTotalLength = string.Empty;
        private bool _suppressCardSelectionLoad;

        public DriveInfo SelectedDrive
        {
            get => _driveInfo;
            set
            {
                _driveInfo = value;
                Manager.SdPath = value?.RootDirectory.ToString();
                _selectedDriveVolumeLabel = string.Empty;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(SelectedDriveVolumeLabel));
                if (value != null)
                    _ = UpdateVolumeLabelAsync(value);
            }
        }

        public string SelectedDriveVolumeLabel => _selectedDriveVolumeLabel;

        public CardRecord SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (ReferenceEquals(_selectedCard, value))
                    return;
                if (_selectedCard != null && value != null && _selectedCard.Id == value.Id)
                {
                    _selectedCard = value;
                    RaisePropertyChanged();
                    return;
                }

                _selectedCard = value;
                RaisePropertyChanged();
                if (!_suppressCardSelectionLoad)
                    _ = LoadSelectedCardGamesAsync();
            }
        }

        public string CardGamesTotalLength
        {
            get => _cardGamesTotalLength;
            private set
            {
                _cardGamesTotalLength = value;
                RaisePropertyChanged();
            }
        }

        private NPath _tempFolder;

        private NPath TempFolder
        {
            get => _tempFolder;
            set
            {
                _tempFolder = value;
                RaisePropertyChanged();
                PersistUserSettings();
            }
        }

        private string _libraryPath = string.Empty;

        private string LibraryPath
        {
            get => _libraryPath;
            set
            {
                _libraryPath = NormalizeLibraryPath(value);
                RaisePropertyChanged();
                PersistUserSettings();
            }
        }

        private static string NormalizeLibraryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Trim() == ".")
                return string.Empty;
            return path.Trim();
        }

        private string _totalFilesLength;

        public string TotalFilesLength
        {
            get => _totalFilesLength;
            private set
            {
                _totalFilesLength = value;
                RaisePropertyChanged();
            }
        }

        public MenuKind MenuKindSelected
        {
            get => Manager.MenuKindSelected;
            set
            {
                Manager.MenuKindSelected = value;
                RaisePropertyChanged();
                PersistUserSettings();
            }
        }

        private string _filter;

        private string Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                RaisePropertyChanged();
            }
        }

        private bool _loadingUserSettings;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            //this.AttachDevTools();
            //this.OpenDevTools();
#endif

            var compressedFileFormats = new string[] { ".7z", ".rar", ".zip" };
            Manager = GDMENUOrganizer.Core.Manager.CreateInstance(
                new DependencyManager(),
                compressedFileFormats
            );

            this.Opened += async (ss, ee) =>
            {
                await AppDatabase.EnsureCreatedAsync();
                await FillDriveListAsync();
                await ReloadCardsAsync();
                await LoadLibraryFromDbAsync();
            };

            CardGames.CollectionChanged += (_, _) => UpdateCardGamesTotalSize();

            this.Closing += MainWindow_Closing;
            Manager.ItemList.CollectionChanged += ItemList_CollectionChanged;

            //config parsing. all settings are optional and must reverse to default values if missing
            bool.TryParse(ConfigurationManager.AppSettings["ShowAllDrives"], out _showAllDrives);
            bool.TryParse(ConfigurationManager.AppSettings["Debug"], out Manager.DebugEnabled);
            if (
                bool.TryParse(
                    ConfigurationManager.AppSettings["UseBinaryString"],
                    out bool useBinaryString
                )
            )
                Converter.ByteSizeToStringConverter.UseBinaryString = useBinaryString;
            if (int.TryParse(ConfigurationManager.AppSettings["CharLimit"], out int charLimit))
                GdItem.Namemaxlen = Math.Min(255, Math.Max(charLimit, 1));
            if (
                bool.TryParse(
                    ConfigurationManager.AppSettings["TruncateMenuGDI"],
                    out bool truncateMenuGDI
                )
            )
                Manager.TruncateMenuGdi = truncateMenuGDI;

            ApplyUserSettings(UserSettings.Load());
            if (string.IsNullOrWhiteSpace(LibraryPath))
            {
                ShowSettingsTab();
                PersistUserSettings(); // clear NiceIO "." leftovers from settings.json
            }
            Title = "GDMENU Organizer " + Constants.Version;

            //showAllDrives = true;

            DataContext = this;
        }

        private void ApplyUserSettings(UserSettings settings)
        {
            _loadingUserSettings = true;
            try
            {
                TempFolder = string.IsNullOrWhiteSpace(settings.TempFolder)
                    ? Path.GetTempPath()
                    : settings.TempFolder;
                LibraryPath = settings.LibraryPath ?? string.Empty;
                if (settings.MenuKind != MenuKind.None)
                    MenuKindSelected = settings.MenuKind;
            }
            finally
            {
                _loadingUserSettings = false;
            }
        }

        private void ShowSettingsTab()
        {
            var tabs = this.FindControl<TabControl>("mainTabs");
            var settingsTab = this.FindControl<TabItem>("settingsTab");
            if (tabs != null && settingsTab != null)
                tabs.SelectedItem = settingsTab;
        }

        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectionChanged bubbles from child ListBox/DataGrid selections. Ignore those or
            // ReloadCardsAsync ↔ SelectedCard ↔ ListBox will recurse until StackOverflow.
            if (sender is not TabControl tabs)
                return;
            if (e.Source != null && !ReferenceEquals(e.Source, tabs))
                return;
            if (tabs.SelectedItem is not TabItem selected)
                return;

            if (selected.Name == "libraryTab")
            {
                _ = LoadLibraryFromDbAsync();
                if (dg1 != null)
                {
                    // Focus after the tab content is shown so keyboard nav works immediately.
                    Avalonia.Threading.Dispatcher.UIThread.Post(
                        () => dg1.Focus(),
                        Avalonia.Threading.DispatcherPriority.Background
                    );
                }
            }
            else if (selected.Name == "cardsTab")
            {
                _ = ReloadCardsAsync();
            }
        }

        private void PersistUserSettings()
        {
            if (_loadingUserSettings)
                return;

            new UserSettings
            {
                LibraryPath = LibraryPath ?? string.Empty,
                TempFolder = TempFolder?.ToString() ?? string.Empty,
                MenuKind = MenuKindSelected
            }.Save();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            dg1 = this.FindControl<DataGrid>("dg1");
        }

        private void ItemList_CollectionChanged(
            object sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e
        )
        {
            updateTotalSize();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (IsBusy)
                e.Cancel = true;
            else
            {
                PersistUserSettings();
                Manager.ItemList.CollectionChanged -= ItemList_CollectionChanged; //release events
            }
        }

        private void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void updateTotalSize()
        {
            var bsize = ByteSizeLib.ByteSize.FromBytes(Manager.ItemList.Sum(x => x.Length.Bytes));
            TotalFilesLength = Converter.ByteSizeToStringConverter.UseBinaryString
                ? bsize.ToBinaryString()
                : bsize.ToString();
        }

        private void UpdateCardGamesTotalSize()
        {
            var bsize = ByteSizeLib.ByteSize.FromBytes(CardGames.Sum(x => x.Length.Bytes));
            CardGamesTotalLength = Converter.ByteSizeToStringConverter.UseBinaryString
                ? bsize.ToBinaryString()
                : bsize.ToString();
        }

        private async Task LoadLibraryFromDbAsync()
        {
            await AppDatabase.EnsureCreatedAsync();
            var games = await AppDatabase.Instance.Library.ListAsync();

            Manager.ItemList.Clear();
            foreach (var record in games)
                Manager.ItemList.Add(LibraryScanner.ToGdItem(record));
        }

        private async Task ReloadCardsAsync(long? selectCardId = null)
        {
            await AppDatabase.EnsureCreatedAsync();
            var cards = await AppDatabase.Instance.Cards.ListAsync();
            var preferredId = selectCardId ?? SelectedCard?.Id;

            _suppressCardSelectionLoad = true;
            try
            {
                CardList.Clear();
                foreach (var card in cards)
                    CardList.Add(card);

                var next =
                    preferredId != null
                        ? CardList.FirstOrDefault(c => c.Id == preferredId)
                        : CardList.FirstOrDefault();

                // Assign field directly while suppressed so ListBox two-way binding cannot
                // re-enter this method through a bubbled TabControl.SelectionChanged.
                if (!ReferenceEquals(_selectedCard, next))
                {
                    _selectedCard = next;
                    RaisePropertyChanged(nameof(SelectedCard));
                }
            }
            finally
            {
                _suppressCardSelectionLoad = false;
            }

            await LoadSelectedCardGamesAsync();
        }

        private async Task LoadSelectedCardGamesAsync()
        {
            CardGames.Clear();
            if (SelectedCard == null)
            {
                UpdateCardGamesTotalSize();
                return;
            }

            await AppDatabase.EnsureCreatedAsync();
            var games = await AppDatabase.Instance.Cards.GetGamesForCardAsync(SelectedCard.Id);
            foreach (var record in games)
                CardGames.Add(LibraryScanner.ToGdItem(record));
            UpdateCardGamesTotalSize();
        }

        private async Task PersistSelectedCardGamesAsync()
        {
            if (SelectedCard == null)
                return;

            var links = CardGames
                .Where(g => g.LibraryGameId.HasValue)
                .Select(
                    (g, index) =>
                        new CardGameLink
                        {
                            LibraryGameId = g.LibraryGameId!.Value,
                            SortOrder = index
                        }
                )
                .ToList();

            await AppDatabase.Instance.Cards.SetGamesAsync(SelectedCard.Id, links);
        }

        private async Task<string> PromptForTextAsync(
            string title,
            string header,
            string defaultValue = ""
        )
        {
            var dialog = new TextInputWindow(title, header, defaultValue);
            if (!await dialog.ShowDialog<bool>(this))
                return null;
            return string.IsNullOrWhiteSpace(dialog.InputValue) ? null : dialog.InputValue;
        }

        private async void ButtonCardAdd_Click(object sender, RoutedEventArgs e)
        {
            var name = await PromptForTextAsync("New Card", "Enter a name for the card");
            if (name == null)
                return;

            try
            {
                await AppDatabase.EnsureCreatedAsync();
                var id = await AppDatabase.Instance.Cards.CreateAsync(name);
                await ReloadCardsAsync(id);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Create Card",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
        }

        private async void ButtonCardDelete_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null)
                return;

            var confirm = await MessageBoxManager
                .GetMessageBoxStandard(
                    "Delete Card",
                    $"Delete card \"{SelectedCard.Name}\"?\nGames stay in the library.",
                    MsBox.Avalonia.Enums.ButtonEnum.YesNo,
                    MsBox.Avalonia.Enums.Icon.Warning
                )
                .ShowWindowDialogAsync(this);

            if (confirm != MsBox.Avalonia.Enums.ButtonResult.Yes)
                return;

            try
            {
                await AppDatabase.Instance.Cards.DeleteAsync(SelectedCard.Id);
                await ReloadCardsAsync();
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Delete Card",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
        }

        private async void ButtonCardRename_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null)
                return;

            var name = await PromptForTextAsync(
                "Rename Card",
                "Enter a new name for the card",
                SelectedCard.Name
            );
            if (name == null || name == SelectedCard.Name)
                return;

            try
            {
                SelectedCard.Name = name;
                await AppDatabase.Instance.Cards.UpdateAsync(SelectedCard);
                await ReloadCardsAsync(SelectedCard.Id);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Rename Card",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
        }

        private async void ButtonCardGameAdd_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null)
                return;

            try
            {
                await AppDatabase.EnsureCreatedAsync();
                var libraryGames = await AppDatabase.Instance.Library.ListAsync();
                var onCard = CardGames
                    .Where(g => g.LibraryGameId.HasValue)
                    .Select(g => g.LibraryGameId!.Value)
                    .ToHashSet();

                var available = libraryGames
                    .Where(g => !onCard.Contains(g.Id))
                    .Select(LibraryScanner.ToGdItem)
                    .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (available.Count == 0)
                {
                    await MessageBoxManager
                        .GetMessageBoxStandard(
                            "Add Games",
                            libraryGames.Count == 0
                                ? "The library is empty. Refresh the Library tab first."
                                : "All library games are already on this card.",
                            icon: MsBox.Avalonia.Enums.Icon.Info
                        )
                        .ShowWindowDialogAsync(this);
                    return;
                }

                var dialog = new AddGamesToCardWindow(available);
                var selected = await dialog.ShowDialog<List<GdItem>>(this);
                if (selected == null || selected.Count == 0)
                    return;

                foreach (var item in selected)
                    CardGames.Add(item);

                await PersistSelectedCardGamesAsync();
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Add Games",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
        }

        private async void ButtonCardGameRemove_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null || dgCardGames == null)
                return;

            var selected = dgCardGames.SelectedItems.Cast<GdItem>().ToArray();
            if (selected.Length == 0)
                return;

            foreach (var item in selected)
                CardGames.Remove(item);

            await PersistSelectedCardGamesAsync();
        }

        private async void ButtonCardGameMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null || dgCardGames == null)
                return;

            var selectedItems = dgCardGames.SelectedItems.Cast<GdItem>().ToArray();
            if (selectedItems.Length == 0)
                return;

            int moveTo = CardGames.IndexOf(selectedItems.First()) - 1;
            if (moveTo < 0)
                return;

            foreach (var item in selectedItems)
                CardGames.Remove(item);

            foreach (var item in selectedItems)
                CardGames.Insert(moveTo++, item);

            dgCardGames.SelectedItems.Clear();
            foreach (var item in selectedItems)
                dgCardGames.SelectedItems.Add(item);

            await PersistSelectedCardGamesAsync();
        }

        private async void ButtonCardGameMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null || dgCardGames == null)
                return;

            var selectedItems = dgCardGames.SelectedItems.Cast<GdItem>().ToArray();
            if (selectedItems.Length == 0)
                return;

            int moveTo = CardGames.IndexOf(selectedItems.Last()) - selectedItems.Length + 2;
            if (moveTo > CardGames.Count - selectedItems.Length)
                return;

            foreach (var item in selectedItems)
                CardGames.Remove(item);

            foreach (var item in selectedItems)
                CardGames.Insert(moveTo++, item);

            dgCardGames.SelectedItems.Clear();
            foreach (var item in selectedItems)
                dgCardGames.SelectedItems.Add(item);

            await PersistSelectedCardGamesAsync();
        }

        private async void ButtonWriteSdCard_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedCard == null)
                return;

            if (CardGames.Count == 0)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Write SD Card",
                        "This card has no games assigned.",
                        icon: MsBox.Avalonia.Enums.Icon.Warning
                    )
                    .ShowWindowDialogAsync(this);
                return;
            }

            if (MenuKindSelected == MenuKind.None)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Write SD Card",
                        "Select a menu kind in Settings before writing.",
                        icon: MsBox.Avalonia.Enums.Icon.Warning
                    )
                    .ShowWindowDialogAsync(this);
                ShowSettingsTab();
                return;
            }

            await FillDriveListAsync(true);

            var dialog = new WriteSdCardWindow(
                SelectedCard.Name,
                DriveList,
                SelectedDrive,
                async () => await FillDriveListAsync(true)
            );
            var drive = await dialog.ShowDialog<DriveInfo>(this);
            if (drive == null)
                return;

            SelectedDrive = drive;

            // Snapshot card games into ItemList for Manager.Save without mutating CardGames.
            Manager.ItemList.Clear();
            foreach (var record in await AppDatabase.Instance.Cards.GetGamesForCardAsync(SelectedCard.Id))
                Manager.ItemList.Add(LibraryScanner.ToGdItem(record));

            await Save();
        }

        private async Task LoadItemsFromCard()
        {
            IsBusy = true;

            try
            {
                await Manager.LoadItemsFromCard();
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Invalid Folders",
                        $"Problem loading the following folder(s):\n\n{ex.Message}",
                        icon: MsBox.Avalonia.Enums.Icon.Warning
                    )
                    .ShowWindowDialogAsync(this);
            }
            finally
            {
                RaisePropertyChanged(nameof(MenuKindSelected));
                PersistUserSettings();
                IsBusy = false;
            }
        }

        private async Task Save()
        {
            IsBusy = true;
            try
            {
                if (await Manager.Save(TempFolder.ToString()))
                {
                    if (Manager.ItemList.Any(x => x.HasError))
                    {
                        await MessageBoxManager
                            .GetMessageBoxStandard(
                                "Warning",
                                "Some items failed while processing. See the list for error details."
                            )
                            .ShowWindowDialogAsync(this);
                    }
                    else
                    {
                        await MessageBoxManager
                            .GetMessageBoxStandard("Message", "Done!")
                            .ShowWindowDialogAsync(this);
                    }
                }
            }
            catch (Exception ex)
            {
                // @note: perhaps we want to mention if we have some sort of failure that leaves the
                // card in a bad state
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Error",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
            finally
            {
                IsBusy = false;
                updateTotalSize();
            }
        }

        private async void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            if (Manager.DebugEnabled)
            {
                var list = DriveInfo
                    .GetDrives()
                    .Where(x => x.IsReady)
                    .Select(x => $"{x.DriveType}; {x.DriveFormat}; {x.Name}")
                    .ToArray();
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Debug",
                        string.Join(Environment.NewLine, list),
                        icon: MsBox.Avalonia.Enums.Icon.None
                    )
                    .ShowWindowDialogAsync(this);
            }

            await new AboutWindow().ShowDialog(this);
            IsBusy = false;
        }

        private async void ButtonFolder_Click(object sender, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Temporary Folder",
                AllowMultiple = false
            };

            if (await TempFolder.DirectoryExistsAsync())
            {
                var startFolder = await StorageProvider.TryGetFolderFromPathAsync(
                    TempFolder.ToString()
                );
                if (startFolder != null)
                    options.SuggestedStartLocation = startFolder;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            var selectedFolder = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(selectedFolder))
                TempFolder = selectedFolder;
        }

        private void ButtonExplorer_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(
                new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = TempFolder.Combine("GDMENUOrganizer").ToString(SlashMode.Native)
                }
            );
        }

        private async void ButtonLibraryFolder_Click(object sender, RoutedEventArgs e)
        {
            var options = new FolderPickerOpenOptions
            {
                Title = "Select Library Folder",
                AllowMultiple = false
            };

            if (
                !string.IsNullOrEmpty(LibraryPath)
                && Directory.Exists(LibraryPath)
            )
            {
                var startFolder = await StorageProvider.TryGetFolderFromPathAsync(LibraryPath);
                if (startFolder != null)
                    options.SuggestedStartLocation = startFolder;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(options);
            var selectedFolder = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(selectedFolder))
                LibraryPath = selectedFolder;
        }

        private void ButtonLibraryExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LibraryPath))
                return;

            Process.Start(
                new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = LibraryPath
                }
            );
        }

        private async void ButtonLibraryRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LibraryPath))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Library Path",
                        "Set a library folder in Settings before refreshing.",
                        icon: MsBox.Avalonia.Enums.Icon.Warning
                    )
                    .ShowWindowDialogAsync(this);
                ShowSettingsTab();
                return;
            }

            if (!Directory.Exists(LibraryPath))
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Library Path",
                        $"Library folder not found:\n{LibraryPath}",
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
                return;
            }

            IsBusy = true;
            Cursor = new Cursor(StandardCursorType.Wait);
            try
            {
                var result = await LibraryScanner.RefreshAsync(LibraryPath);

                Manager.ItemList.Clear();
                foreach (var record in result.Games)
                    Manager.ItemList.Add(LibraryScanner.ToGdItem(record));

                var summary =
                    $"Present: {result.PresentCount}\nNew: {result.NewCount}\nMissing: {result.MissingCount}";
                if (result.Skipped.Count > 0)
                {
                    summary +=
                        $"\n\nSkipped ({result.Skipped.Count}):\n"
                        + string.Join(Environment.NewLine, result.Skipped.Take(20));
                    if (result.Skipped.Count > 20)
                        summary += $"\n...and {result.Skipped.Count - 20} more";
                }

                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Library Refresh",
                        summary,
                        icon: result.Skipped.Count > 0
                            ? MsBox.Avalonia.Enums.Icon.Warning
                            : MsBox.Avalonia.Enums.Icon.Info
                    )
                    .ShowWindowDialogAsync(this);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Library Refresh",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }
            finally
            {
                Cursor = Cursor.Default;
                IsBusy = false;
            }
        }

        private async void ButtonInfo_Click(object sender, RoutedEventArgs e)
        {
            IsBusy = true;
            try
            {
                var btn = (Button)sender;
                var item = (GdItem)btn.CommandParameter;

                if (item.Ip == null)
                    await Manager.LoadIp(item);

                await new InfoWindow(item).ShowDialog(this);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Error",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }

            IsBusy = false;
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

                if (ReferenceEquals(_driveInfo, drive))
                {
                    _selectedDriveVolumeLabel = label ?? string.Empty;
                    RaisePropertyChanged(nameof(SelectedDriveVolumeLabel));
                }
            }
            catch
            {
            }
        }

        private async Task FillDriveListAsync(bool isRefreshing = false)
        {
            var showAllDrives = _showAllDrives;
            var probe = await Task.Run(() => ProbeDrives(showAllDrives));

            if (isRefreshing)
            {
                if (DriveList.Select(x => x.Name).SequenceEqual(probe.Drives.Select(x => x.Name)))
                    return;

                DriveList.Clear();
            }

            foreach (DriveInfo drive in probe.Drives)
                DriveList.Add(drive);

            if (!DriveList.Any())
                return;

            if (SelectedDrive != null)
                return;

            if (probe.SuggestedDriveName != null)
            {
                var suggested = DriveList.FirstOrDefault(d => d.Name == probe.SuggestedDriveName);
                if (suggested != null)
                {
                    SelectedDrive = suggested;
                    return;
                }
            }

            SelectedDrive = DriveList.LastOrDefault();
        }

        private static DriveProbeResult ProbeDrives(bool showAllDrives)
        {
            DriveInfo[] list;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                list = DriveInfo
                    .GetDrives()
                    .Where(
                        x =>
                            SafeIsReady(x)
                            && (
                                showAllDrives
                                || (
                                    x.DriveType == DriveType.Removable
                                    && SafeDriveFormat(x).StartsWith("FAT")
                                )
                            )
                    )
                    .ToArray();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                list = DriveInfo
                    .GetDrives()
                    .Where(
                        x =>
                            SafeIsReady(x)
                            && (
                                showAllDrives
                                || x.DriveType == DriveType.Removable
                                || x.DriveType == DriveType.Fixed
                                || (
                                    x.DriveType == DriveType.Unknown
                                    && SafeDriveFormat(x)
                                        .Equals("lifs", StringComparison.InvariantCultureIgnoreCase)
                                )
                            )
                    )
                    .ToArray();
            else //linux
                list = DriveInfo
                    .GetDrives()
                    .Where(
                        x =>
                            SafeIsReady(x)
                            && (
                                showAllDrives
                                || (
                                    (
                                        x.DriveType == DriveType.Removable
                                        || x.DriveType == DriveType.Fixed
                                    )
                                    && SafeDriveFormat(x)
                                        .Equals(
                                            "msdos",
                                            StringComparison.InvariantCultureIgnoreCase
                                        )
                                    && (
                                        x.Name.StartsWith(
                                            "/media/",
                                            StringComparison.InvariantCultureIgnoreCase
                                        )
                                        || x.Name.StartsWith(
                                            "/run/media/",
                                            StringComparison.InvariantCultureIgnoreCase
                                        )
                                    )
                                )
                            )
                    )
                    .ToArray();

            var drives = new List<DriveInfo>();
            string suggestedDriveName = null;

            foreach (DriveInfo drive in list)
            {
                try
                {
                    drives.Add(drive);
                    if (
                        suggestedDriveName == null
                        && File.Exists(
                            Path.Combine(
                                drive.RootDirectory.FullName,
                                Constants.MenuConfigTextFile
                            )
                        )
                    )
                        suggestedDriveName = drive.Name;
                }
                catch
                {
                }
            }

            if (suggestedDriveName == null)
            {
                foreach (DriveInfo drive in list)
                {
                    try
                    {
                        if (Directory.Exists(Path.Combine(drive.RootDirectory.FullName, "01")))
                        {
                            suggestedDriveName = drive.Name;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (suggestedDriveName == null)
            {
                foreach (DriveInfo drive in list)
                {
                    try
                    {
                        if (
                            drive.Name.StartsWith(
                                "/media/",
                                StringComparison.InvariantCultureIgnoreCase
                            )
                        )
                        {
                            suggestedDriveName = drive.Name;
                            break;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (suggestedDriveName == null && drives.Count > 0)
                suggestedDriveName = drives[drives.Count - 1].Name;

            return new DriveProbeResult(drives, suggestedDriveName);
        }

        private static bool SafeIsReady(DriveInfo drive)
        {
            try
            {
                return drive.IsReady;
            }
            catch
            {
                return false;
            }
        }

        private static string SafeDriveFormat(DriveInfo drive)
        {
            try
            {
                return drive.DriveFormat ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class DriveProbeResult
        {
            public DriveProbeResult(List<DriveInfo> drives, string suggestedDriveName)
            {
                Drives = drives;
                SuggestedDriveName = suggestedDriveName;
            }

            public List<DriveInfo> Drives { get; }
            public string SuggestedDriveName { get; }
        }

        private async void MenuItemRename_Click(object sender, RoutedEventArgs e)
        {
            var menuitem = (MenuItem)sender;
            var item = (GdItem)menuitem.CommandParameter;

            var msBox = MessageBoxManager.GetMessageBoxCustom(
                new MsBox.Avalonia.Dto.MessageBoxCustomParams
                {
                    ContentTitle = "Rename",
                    ContentHeader = "inform new name",
                    ContentMessage = "Name",
                    InputParams = new MsBox.Avalonia.Dto.InputParams
                    {
                        DefaultValue = item.Name ?? string.Empty,
                        Multiline = false
                    },
                    ShowInCenter = true,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ButtonDefinitions = new ButtonDefinition[]
                    {
                        new ButtonDefinition { Name = "Ok" },
                        new ButtonDefinition { Name = "Cancel" }
                    }
                }
            );
            var result = await msBox.ShowWindowDialogAsync(this);

            if (result == "Ok" && !string.IsNullOrWhiteSpace(msBox.InputValue))
                item.Name = msBox.InputValue.Trim();
        }

        private void MenuItemRenameSentence_Click(object sender, RoutedEventArgs e)
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            IEnumerable<GdItem> items = Enumerable.Cast<GdItem>(dg1.SelectedItems);

            foreach (var item in items)
            {
                item.Name = textInfo.ToTitleCase(textInfo.ToLower(item.Name));
            }
        }

        private async void MenuItemRenameIP_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.Ip);
        }

        private async void MenuItemRenameFolder_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.Folder);
        }

        private async void MenuItemRenameFile_Click(object sender, RoutedEventArgs e)
        {
            await renameSelection(RenameBy.File);
        }

        private async Task renameSelection(RenameBy renameBy)
        {
            IsBusy = true;
            try
            {
                await Manager.RenameItems(Enumerable.Cast<GdItem>(dg1.SelectedItems), renameBy);
            }
            catch (Exception ex)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(
                        "Error",
                        ex.Message,
                        icon: MsBox.Avalonia.Enums.Icon.Error
                    )
                    .ShowWindowDialogAsync(this);
            }

            IsBusy = false;
        }

        //private void rename(GdItem item, short index)
        //{
        //    string name;

        //    if (index == 0)//ip.bin
        //    {
        //        name = item.Ip.Name;
        //    }
        //    else
        //    {
        //        if (index == 1)//folder
        //            name = Path.GetFileName(item.FullFolderPath).ToUpperInvariant();
        //        else//file
        //            name = Path.GetFileNameWithoutExtension(item.ImageFile).ToUpperInvariant();
        //        var m = RegularExpressions.TosecnNameRegexp.Match(name);
        //        if (m.Success)
        //            name = name.Substring(0, m.Index);
        //    }
        //    item.Name = name;
        //}

        //private void rename(object sender, short index)
        //{
        //    var menuItem = (MenuItem)sender;
        //    var item = (GdItem)menuItem.CommandParameter;

        //    string name;

        //    if (index == 0)//ip.bin
        //    {
        //        name = item.Ip.Name;
        //    }
        //    else
        //    {
        //        if (index == 1)//folder
        //            name = Path.GetFileName(item.FullFolderPath).ToUpperInvariant();
        //        else//file
        //            name = Path.GetFileNameWithoutExtension(item.ImageFile).ToUpperInvariant();
        //        var m = RegularExpressions.TosecnNameRegexp.Match(name);
        //        if (m.Success)
        //            name = name.Substring(0, m.Index);
        //    }
        //    item.Name = name;
        //}

        private async void ButtonSearch_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.ItemList.Count == 0 || string.IsNullOrWhiteSpace(Filter))
                return;

            try
            {
                IsBusy = true;
                await Manager.LoadIpAll();
                IsBusy = false;
            }
            catch (ProgressWindowClosedException) { }

            if (dg1.SelectedIndex == -1 || !searchInGrid(dg1.SelectedIndex))
                searchInGrid(0);
        }

        private bool searchInGrid(int start)
        {
            for (int i = start; i < Manager.ItemList.Count; i++)
            {
                var item = Manager.ItemList[i];
                if (dg1.SelectedItem != item && Manager.SearchInItem(item, Filter))
                {
                    dg1.SelectedItem = item;
                    dg1.ScrollIntoView(item, null);
                    return true;
                }
            }

            return false;
        }

        private async void ButtonExportList_Click(object sender, RoutedEventArgs eventArgs)
        {
            var storageFile = await StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("JSON File") { Patterns = new[] { "*.json" } }
                    }
                }
            );
            var file = storageFile?.TryGetLocalPath();
            if (file == null)
                return;

            var exportFileManager = new ExportFileManager(file);
            await exportFileManager.WriteItems(Manager.ItemList);
        }
    }
}
