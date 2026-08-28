using System.IO;
using System.Windows;
using WinCopyQueue.Core;
using Clipboard = System.Windows.Clipboard;

namespace WinCopyQueue;

public static class ClipboardTransferReader
{
    public static bool TryRead(out TransferOperation operation, out IReadOnlyList<string> sources)
    {
        operation = TransferOperation.Copy;
        sources = [];

        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!Clipboard.ContainsFileDropList())
                {
                    return false;
                }

                sources = Clipboard.GetFileDropList()
                    .Cast<string>()
                    .Where(path => File.Exists(path) || Directory.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                operation = ReadOperation();
                return sources.Count > 0;
            }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 5)
            {
                Thread.Sleep(40);
            }
        }

        return false;
    }

    public static void TryClear()
    {
        try { Clipboard.Clear(); }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private static TransferOperation ReadOperation()
    {
        try
        {
            var data = Clipboard.GetDataObject()?.GetData("Preferred DropEffect");
            var bytes = data switch
            {
                MemoryStream stream => stream.ToArray(),
                byte[] array => array,
                _ => []
            };

            return bytes.Length >= 4 && (BitConverter.ToInt32(bytes, 0) & 2) == 2
                ? TransferOperation.Move
                : TransferOperation.Copy;
        }
        catch
        {
            return TransferOperation.Copy;
        }
    }
}
