using System;
using CipherShare.Common;

namespace CipherShare.Models;

public class NotificationModel : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NotificationType Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    private bool _isRead;
    public bool IsRead
    {
        get => _isRead;
        set => Set(ref _isRead, value);
    }
}
