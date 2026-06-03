using System.Text;

namespace PointBlankPanel.Helpers;

internal static class StrObf
{
    private static readonly byte _xor = 0x7B;

    public static string Get(byte[] data)
    {
        var buf = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            buf[i] = (byte)(data[i] ^ _xor);
        return Encoding.UTF8.GetString(buf);
    }
}
