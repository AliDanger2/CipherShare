namespace CipherShare.Models;

/// <summary>
/// One file inside a transfer. RelativePath preserves folder structure
/// (e.g. "photos/beach.jpg") so whole folders can be sent, not just single files.
/// </summary>
public class TransferFileEntry
{
    public string RelativePath { get; set; }
    public long Size { get; set; }

    public override string ToString() => RelativePath;
}
