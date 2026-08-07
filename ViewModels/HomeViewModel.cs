using KlasorKasa.Infrastructure;

namespace KlasorKasa.ViewModels;

public sealed class HomeViewModel : ObservableObject
{
    private int _total;
    private int _locked;
    private int _open;
    public int Total { get => _total; set => SetProperty(ref _total, value); }
    public int Locked { get => _locked; set => SetProperty(ref _locked, value); }
    public int Open { get => _open; set => SetProperty(ref _open, value); }
}
