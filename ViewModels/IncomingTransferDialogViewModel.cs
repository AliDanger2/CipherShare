using System;
using System.Windows.Input;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class IncomingTransferDialogViewModel : ObservableObject
{
    private readonly AppState _state;

    public TransferRequestModel Request { get; }

    public string SenderLabel => $"{Request.SenderName} ({Request.SenderIp})";
    public int FileCount => Request.Files.Count;
    public long TotalSize => Request.TotalSize;

    public ICommand AcceptCommand { get; }
    public ICommand DeclineCommand { get; }

    public event Action RequestClose;

    public IncomingTransferDialogViewModel(AppState state, TransferRequestModel request)
    {
        _state = state;
        Request = request;

        AcceptCommand = new RelayCommand(_ =>
        {
            _state.RespondToTransferRequest(Request, true);
            RequestClose?.Invoke();
        });

        DeclineCommand = new RelayCommand(_ =>
        {
            _state.RespondToTransferRequest(Request, false);
            RequestClose?.Invoke();
        });
    }
}
