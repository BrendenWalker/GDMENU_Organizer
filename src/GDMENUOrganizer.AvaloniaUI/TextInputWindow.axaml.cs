using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace GDMENUOrganizer
{
    public partial class TextInputWindow : Window
    {
        private readonly TextBox _tbInput;

        public string InputValue => _tbInput.Text?.Trim() ?? string.Empty;

        public TextInputWindow()
            : this("Input", string.Empty, string.Empty)
        {
        }

        public TextInputWindow(string title, string header, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;

            var tbHeader = this.FindControl<TextBlock>("tbHeader")
                ?? throw new System.InvalidOperationException("tbHeader not found.");
            _tbInput = this.FindControl<TextBox>("tbInput")
                ?? throw new System.InvalidOperationException("tbInput not found.");

            tbHeader.Text = header;
            _tbInput.Text = defaultValue ?? string.Empty;

            Opened += (_, _) =>
            {
                Dispatcher.UIThread.Post(
                    () =>
                    {
                        _tbInput.Focus();
                        _tbInput.SelectAll();
                    },
                    DispatcherPriority.Input
                );
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_tbInput.Text))
                return;
            Close(true);
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
