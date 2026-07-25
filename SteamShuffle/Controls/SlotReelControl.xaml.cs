using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Controls;

public partial class SlotReelControl
{
    private const double SlotWidth = 220;   // total width reserved per reel item, including margin
    private const double ItemWidth = 200;
    private const double ItemHeight = 220;

    private static readonly Random Rng = new();

    public event EventHandler<SteamGame>? SpinCompleted;

    public SlotReelControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Spins the reel through a shuffled sequence of <paramref name="pool"/> and
    /// eases to a stop on <paramref name="winner"/>, centered under the pointer.
    /// </summary>
    public void Spin(IReadOnlyList<SteamGame> pool, SteamGame winner)
    {
        if (pool.Count == 0)
        {
            return;
        }

        ReelStrip.Children.Clear();

        // Build a long, shuffled sequence so the reel has plenty to scroll through,
        // then place the winner a comfortable distance from the end so the
        // deceleration has room to play out.
        var sequence = new List<SteamGame>();
        for (int lap = 0; lap < 6; lap++)
            sequence.AddRange(Shuffle(pool));

        const int tailBuffer = 4;
        int winnerIndex = Math.Max(0, sequence.Count - tailBuffer);
        sequence.Insert(winnerIndex, winner);

        foreach (var game in sequence)
            ReelStrip.Children.Add(BuildReelItem(game));

        ReelStrip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double viewportWidth = ReelClip.ActualWidth > 0 ? ReelClip.ActualWidth : ActualWidth;
        double centerOffset = viewportWidth / 2 - ItemWidth / 2;

        // Start position: first item roughly under the pointer.
        ReelTransform.X = centerOffset;

        double targetX = centerOffset - winnerIndex * SlotWidth;

        var animation = new DoubleAnimation
        {
            From = centerOffset,
            To = targetX,
            Duration = TimeSpan.FromSeconds(3.6),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        animation.Completed += (_, _) => SpinCompleted?.Invoke(this, winner);
        ReelTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private static Border BuildReelItem(SteamGame game)
    {
        var image = new Image
        {
            Width = ItemWidth,
            Height = ItemHeight,
            Stretch = Stretch.UniformToFill,
        };

        try
        {
            image.Source = new BitmapImage(new Uri(game.CapsuleImageUrl));
        }
        catch
        {
            // Missing/broken art shouldn't crash the spin — leave it blank.
        }

        return new Border
        {
            Width = ItemWidth,
            Height = ItemHeight,
            Margin = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(40, 44, 54)),
            Child = image,
        };
    }

    private static List<SteamGame> Shuffle(IReadOnlyList<SteamGame> source)
    {
        var list = source.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}