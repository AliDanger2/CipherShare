using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class TransferQueueViewModel : ObservableObject
{
    public AppState State { get; }

    public ObservableCollection<TransferModel> Transfers => State.Transfers;

    private string _directionFilter = "All";
    /// <summary>"All", "Sent" or "Received".</summary>
    public string DirectionFilter
    {
        get => _directionFilter;
        set
        {
            if (Set(ref _directionFilter, value)) OnPropertyChanged(nameof(FilteredTransfers));
        }
    }

    public List<TransferModel> FilteredTransfers => State.Transfers
        .Where(t => t.Status is TransferStatus.Pending or TransferStatus.Active or TransferStatus.Paused)
        .Where(MatchesDirectionFilter)
        .OrderByDescending(t => t.StartedAtUtc)
        .ToList();

    public int ActiveCount => State.Transfers.Count(t => t.Status == TransferStatus.Active);
    public int PausedCount => State.Transfers.Count(t => t.Status == TransferStatus.Paused);
    public int PendingCount => State.Transfers.Count(t => t.Status == TransferStatus.Pending);

    public System.Windows.Input.ICommand DirectionFilterCommand { get; }

    public TransferQueueViewModel(AppState state)
    {
        State = state;
        DirectionFilterCommand = new RelayCommand(p => { if (p is string s) DirectionFilter = s; });
        foreach (var transfer in State.Transfers) transfer.PropertyChanged += Transfer_PropertyChanged;
        State.Transfers.CollectionChanged += Transfers_CollectionChanged;
    }

    private bool MatchesDirectionFilter(TransferModel t)
    {
        return DirectionFilter switch
        {
            "Sent" => t.Direction == TransferDirection.Sent,
            "Received" => t.Direction == TransferDirection.Received,
            _ => true
        };
    }

    private void Transfers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TransferModel t in e.NewItems) t.PropertyChanged += Transfer_PropertyChanged;
        if (e.OldItems != null)
            foreach (TransferModel t in e.OldItems) t.PropertyChanged -= Transfer_PropertyChanged;

        RaiseComputedChanged();
    }

    private void Transfer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferModel.Status))
        {
            RaiseComputedChanged();
        }
    }

    private void RaiseComputedChanged()
    {
        OnPropertyChanged(nameof(FilteredTransfers));
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(PausedCount));
        OnPropertyChanged(nameof(PendingCount));
    }
}
