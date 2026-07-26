using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using SteamShuffle.CoreModels;

namespace SteamShuffle.Controls;

public partial class SlotReelControl : UserControl
{
    private const double SlotWidth = 220;   // total width reserved per reel item, including margin
    private const double ItemWidth = 200;
    private const double ItemHeight = 300;   // matches the 2:3 capsule art aspect ratio, so UniformToFill doesn't crop

    // Minimum number of slots required between two tiles for the same game,
    // wide enough that a viewport-full of tiles never shows the same game twice.
    private const int MinDuplicateGap = 6;

    private static readonly Random Rng = new();

    // Reused across all cover-art downloads for this control; a fresh HttpClient
    // per image would exhaust sockets under heavy use.
    private static readonly System.Net.Http.HttpClient ImageHttp = new();

    private readonly TranslateTransform _reelTransform = new();

    public SlotReelControl()
    {
        InitializeComponent();
        ReelStrip.RenderTransform = _reelTransform;
    }

    /// <summary>
    /// Spins the reel through a shuffled sequence of <paramref name="pool"/> and
    /// eases to a stop on <paramref name="winner"/>, centered under the pointer.
    /// </summary>
    public async Task<SteamGame> SpinAsync(IReadOnlyList<SteamGame> pool, SteamGame winner)
    {
        if (pool.Count == 0)
        {
            return winner;
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

        double viewportWidth = ReelClip.Bounds.Width > 0 ? ReelClip.Bounds.Width : Bounds.Width;
        double centerOffset = viewportWidth / 2 - ItemWidth / 2;
        double targetX = centerOffset - winnerIndex * SlotWidth;

        _reelTransform.X = centerOffset;

        // Avalonia's Animation.RunAsync returns a Task directly; no
        // Completed-event subscribe/unsubscribe dance like WPF's Storyboard needed.
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(3.6),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(TranslateTransform.XProperty, centerOffset) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(TranslateTransform.XProperty, targetX) } },
            },
        };

        // TransformAnimator requires the visual that owns the RenderTransform, not the
        // Transform object itself -- passing _reelTransform directly throws
        // InvalidCastException trying to cast it to Visual.
        await animation.RunAsync(ReelStrip);
        _reelTransform.X = targetX; // belt-and-braces; FillMode.Forward should already leave it here

        return winner;
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
            FontWeight = FontWeight.SemiBold,
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

        _ = LoadCoverArtAsync(image, game);

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
            [ToolTip.TipProperty] = game.Name,
            Child = grid,
        };
    }

    /// <summary>
    /// Avalonia's Bitmap only loads from a Stream (WPF's BitmapImage could just be
    /// pointed at a Uri and it handled the async fetch itself), so cover art is
    /// fetched by hand here. Falls back to the store's wide header banner
    /// (letterboxed via Stretch.Uniform, since it isn't cut for a tall capsule
    /// slot) if the capsule art itself fails to load.
    /// </summary>
    private static async Task LoadCoverArtAsync(Image image, SteamGame game)
    {
        if (await TrySetSourceAsync(image, game.CapsuleImageUrl, Stretch.UniformToFill))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(game.HeaderImageUrl))
        {
            await TrySetSourceAsync(image, game.HeaderImageUrl, Stretch.Uniform);
        }
    }

    private static async Task<bool> TrySetSourceAsync(Image image, string? url, Stretch stretch)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            // Avalonia's Bitmap(Stream) goes through Skia, which needs a seekable
            // stream to sniff the image format -- the raw network stream from
            // GetStreamAsync isn't seekable and fails to decode, so buffer it first.
            byte[] bytes = await ImageHttp.GetByteArrayAsync(url);
            using var memoryStream = new MemoryStream(bytes);
            var bitmap = new Bitmap(memoryStream);

            // Bitmap decoding can happen off-thread, but Image.Source must be
            // assigned on the UI thread.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                image.Stretch = stretch;
                image.Source = bitmap;
            });
            return true;
        }
        catch
        {
            // Missing/broken art shouldn't crash the spin — leave it blank.
            return false;
        }
    }

    /// <summary>
    /// Ensures no two tiles for the same game land within <paramref name="minGap"/>
    /// slots of each other. Each lap is shuffled independently and the pool
    /// itself always contains the winner, so without this a second copy of the
    /// winner's game (or any other) can land just a few tiles from where it
    /// stops. The winner's own slot is never moved, since <paramref name="winnerIndex"/>
    /// is where the reel physically stops.
    ///
    /// Runs forward then backward: a single forward pass only catches a copy
    /// that comes *after* an earlier one, so an earlier movable copy sitting
    /// right before the (immovable) winner would otherwise never get relocated
    /// away from it. The backward pass catches exactly that case.
    /// </summary>
    private static void DeduplicateNearby(List<SteamGame> sequence, int winnerIndex, int minGap)
    {
        DeduplicateDirectional(sequence, winnerIndex, minGap, step: 1);
        DeduplicateDirectional(sequence, winnerIndex, minGap, step: -1);
    }

    private static void DeduplicateDirectional(List<SteamGame> sequence, int winnerIndex, int minGap, int step)
    {
        var seenIndex = new Dictionary<int, int>();
        int start = step > 0 ? 0 : sequence.Count - 1;

        for (int i = start; i >= 0 && i < sequence.Count; i += step)
        {
            int appId = sequence[i].AppId;
            bool tooClose = seenIndex.TryGetValue(appId, out int seen) && Math.Abs(i - seen) < minGap;

            if (tooClose && i != winnerIndex)
            {
                int swapWith = FindSwapCandidate(sequence, i, winnerIndex, minGap, seenIndex, step);
                if (swapWith != -1)
                {
                    (sequence[i], sequence[swapWith]) = (sequence[swapWith], sequence[i]);
                    appId = sequence[i].AppId;
                }
            }

            seenIndex[appId] = i;
        }
    }

    /// <summary>Finds the nearest slot (searching in <paramref name="step"/> direction) that can swap into <paramref name="i"/> without creating a new too-close pair.</summary>
    private static int FindSwapCandidate(List<SteamGame> sequence, int i, int winnerIndex, int minGap, Dictionary<int, int> seenIndex, int step)
    {
        for (int j = i + step; j >= 0 && j < sequence.Count; j += step)
        {
            if (j == winnerIndex || sequence[j].AppId == sequence[i].AppId)
            {
                continue;
            }

            bool candidateStillTooClose = seenIndex.TryGetValue(sequence[j].AppId, out int candidateSeen)
                                           && Math.Abs(i - candidateSeen) < minGap;

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
