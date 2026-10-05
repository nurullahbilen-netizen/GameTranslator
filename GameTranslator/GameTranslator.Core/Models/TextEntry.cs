using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameTranslator.Core.Models;

public enum EntryStatus { Pending, Translated, Failed }

public sealed class TextEntry : INotifyPropertyChanged
{
    public required string File { get; init; }
    public required string Key { get; init; }
    public required string Source { get; init; }

    private string? _translation;
    private EntryStatus _status;

    public string? Translation { get => _translation; set => Set(ref _translation, value); }
    public EntryStatus Status { get => _status; set => Set(ref _status, value); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
