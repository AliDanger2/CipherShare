using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class HistoryViewModel : ObservableObject
{
    public AppState State { get; }

    public ObservableCollection<TransferModel> Transfers => State.Transfers;

    private string _statusFilter = "All";
    /// <summary>"All", "Completed", "Failed" or "Canceled".</summary>
    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (Set(ref _statusFilter, value)) OnPropertyChanged(nameof(FilteredTransfers));
        }
    }

    public List<TransferModel> FilteredTransfers => State.Transfers
        .Where(t => t.Status is TransferStatus.Completed or TransferStatus.Failed or TransferStatus.Canceled)
        .Where(MatchesStatusFilter)
        .OrderByDescending(t => t.CompletedAtUtc ?? t.StartedAtUtc)
        .ToList();

    public System.Windows.Input.ICommand StatusFilterCommand { get; }

    public HistoryViewModel(AppState state)
    {
        State = state;
        StatusFilterCommand = new RelayCommand(p => { if (p is string s) StatusFilter = s; });
        foreach (var transfer in State.Transfers) transfer.PropertyChanged += Transfer_PropertyChanged;
        State.Transfers.CollectionChanged += Transfers_CollectionChanged;
    }

    private bool MatchesStatusFilter(TransferModel t)
    {
        return StatusFilter switch
        {
            "Completed" => t.Status == TransferStatus.Completed,
            "Failed" => t.Status == TransferStatus.Failed,
            "Canceled" => t.Status == TransferStatus.Canceled,
            _ => true
        };
    }

    private void Transfers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TransferModel t in e.NewItems) t.PropertyChanged += Transfer_PropertyChanged;
        if (e.OldItems != null)
            foreach (TransferModel t in e.OldItems) t.PropertyChanged -= Transfer_PropertyChanged;

        OnPropertyChanged(nameof(FilteredTransfers));
    }

    private void Transfer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferModel.Status))
        {
            OnPropertyChanged(nameof(FilteredTransfers));
        }
    }
}
