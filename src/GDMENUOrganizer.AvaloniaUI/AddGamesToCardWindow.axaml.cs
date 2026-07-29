using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using GDMENUOrganizer.Core;

namespace GDMENUOrganizer
{
    public partial class AddGamesToCardWindow : Window
    {
        private readonly DataGrid _dgLibrary;

        public ObservableCollection<GdItem> AvailableGames { get; }

        public AddGamesToCardWindow()
            : this(Array.Empty<GdItem>())
        {
        }

        public AddGamesToCardWindow(IEnumerable<GdItem> availableGames)
        {
            AvailableGames = new ObservableCollection<GdItem>(availableGames);
            InitializeComponent();
            DataContext = this;
            _dgLibrary = this.FindControl<DataGrid>("dgLibrary")
                ?? throw new InvalidOperationException("dgLibrary not found.");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void ButtonAdd_Click(object sender, RoutedEventArgs e)
        {
            var selected = _dgLibrary.SelectedItems.Cast<GdItem>().ToList();
            Close(selected.Count > 0 ? selected : null);
        }
    }
}
