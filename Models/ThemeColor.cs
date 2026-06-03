using System.Windows.Media;

namespace PointBlankPanel.Models;

public class ThemeColor
{
    public string Name { get; init; } = "";
    public Color Accent { get; }
    public Color Light { get; }
    public Color G1 { get; }
    public Color G2 { get; }
    public Color G3 { get; }
    public Color G4 { get; }
    public Brush AccentBrush => new SolidColorBrush(Accent);

    public ThemeColor(string name, Color accent, Color light, Color g1, Color g2, Color g3, Color g4)
    {
        Name = name; Accent = accent; Light = light; G1 = g1; G2 = g2; G3 = g3; G4 = g4;
    }

    public override bool Equals(object? obj) => obj is ThemeColor t && t.Accent == Accent;
    public override int GetHashCode() => Accent.GetHashCode();
}
