using System.Windows.Input;
using KlasorKasa.Infrastructure;

namespace KlasorKasa.ViewModels;

public sealed class CreateVaultViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _folderPath = string.Empty;
    private string _errorMessage = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (!SetProperty(ref _folderPath, value)) return;
            Name = string.IsNullOrWhiteSpace(value) ? string.Empty : Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar));
            ErrorMessage = string.Empty;
            OnPropertyChanged(nameof(HasSelection));
        }
    }
    public bool HasSelection => Directory.Exists(FolderPath);
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
    public ICommand BrowseCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public event EventHandler<bool>? CloseRequested;

    public CreateVaultViewModel()
    {
        BrowseCommand = new RelayCommand(Browse);
        CreateCommand = new RelayCommand(Create);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
    }
    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Korunacak klasörü seçin", Multiselect = false };
        if (dialog.ShowDialog() == true) FolderPath = dialog.FolderName;
    }
    private void Create()
    {
        if (!Directory.Exists(FolderPath)) { ErrorMessage = "Korunacak klasörü seçin."; return; }
        Name = Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Bu klasör kasa olarak kullanılamaz."; return; }
        CloseRequested?.Invoke(this, true);
    }
}
