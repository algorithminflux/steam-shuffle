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
    private const double ItemHeight = 300;   // matches the 2:3 capsule art aspect ratio, so UniformToFill doesn't crop

    // Minimum number of slots required between two tiles for the same game,
    // wide enough that a viewport-full of tiles never shows the same game twice.
    private const int MinDuplicateGap = 6;

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

        DeduplicateNearby(sequence, winnerIndex, MinDuplicateGap);

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
        // Sits behind the image so a game with no working art still shows its
        // name instead of a blank tile (library capsule art doesn't exist for
        // every app, and the store header image can be missing too).
        var nameFallback = new TextBlock
        {
            Text = game.Name,
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10),
        };

        var image = new Image
        {
            Width = ItemWidth,
            Height = ItemHeight,
            Stretch = Stretch.UniformToFill,
        };

        bool triedHeaderFallback = false;
        image.ImageFailed += (_, _) =>
        {
            if (triedHeaderFallback || string.IsNullOrWhiteSpace(game.HeaderImageUrl))
            {
                image.Source = null;
                return;
            }

            triedHeaderFallback = true;
            TrySetSource(image, game.HeaderImageUrl);
        };

        TrySetSource(image, game.CapsuleImageUrl);

        var grid = new Grid { Width = ItemWidth, Height = ItemHeight };
        grid.Children.Add(nameFallback);
        grid.Children.Add(image);

        return new Border
        {
            Width = ItemWidth,
            Height = ItemHeight,
            Margin = new Thickness(10, 0, 10, 0),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(40, 44, 54)),
            ToolTip = game.Name,
            Child = grid,
        };
    }

    private static void TrySetSource(Image image, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            image.Source = null;
            return;
        }

        try
        {
            image.Source = new BitmapImage(new Uri(url));
        }
        catch
        {
            // Missing/broken art shouldn't crash the spin — leave it blank.
            image.Source = null;
        }
    }

    /// <summary>
    /// Ensures no two tiles for the same game land within <paramref name="minGap"/>
    /// slots of each other. Each lap is shuffled independently and the pool
    /// itself always contains the winner, so without this a second copy of the
    /// winner's game (or any other) can land just a few tiles from where it
    /// stops. The winner's own slot is never moved, since <paramref name="winnerIndex"/>
    /// is where the reel physically stops.
    /// </summary>
    private static void DeduplicateNearby(List<SteamGame> sequence, int winnerIndex, int minGap)
    {
        var lastSeenIndex = new Dictionary<int, int>();

        for (int i = 0; i < sequence.Count; i++)
        {
            int appId = sequence[i].AppId;
            bool tooClose = lastSeenIndex.TryGetValue(appId, out int prevIndex) && i - prevIndex < minGap;

            if (tooClose && i != winnerIndex)
            {
                int swapWith = FindSwapCandidate(sequence, i, winnerIndex, minGap, lastSeenIndex);
                if (swapWith != -1)
                {
                    (sequence[i], sequence[swapWith]) = (sequence[swapWith], sequence[i]);
                    appId = sequence[i].AppId;
                }
            }

            lastSeenIndex[appId] = i;
        }
    }

    /// <summary>Finds the nearest later slot that can swap into <paramref name="i"/> without creating a new too-close pair.</summary>
    private static int FindSwapCandidate(List<SteamGame> sequence, int i, int winnerIndex, int minGap, Dictionary<int, int> lastSeenIndex)
    {
        for (int j = i + 1; j < sequence.Count; j++)
        {
            if (j == winnerIndex || sequence[j].AppId == sequence[i].AppId)
            {
                continue;
            }

            bool candidateStillTooClose = lastSeenIndex.TryGetValue(sequence[j].AppId, out int candidatePrevIndex)
                                           && i - candidatePrevIndex < minGap;

            if (!candidateStillTooClose)
            {
                return j;
            }
        }

        return -1;
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