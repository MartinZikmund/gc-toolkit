using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GcToolkit.Core.Alphabets;
using GcToolkit.Core.Catalog;
using GcToolkit.Core.Discovery;
using GcToolkit.Core.FavoriteTools;
using GcToolkit.Core.Recents;
using GcToolkit.Core.Services;
using Microsoft.Extensions.Localization;

namespace GcToolkit.Core.ViewModels.Tools;

/// <summary>
/// Bidirectional Morse converter (issue #53). Encodes/decodes live as the user types, copies/shares the
/// result, and plays the code back as an on-screen flash (all heads) plus an optional tone (heads where
/// <see cref="IMorseAudioService.IsSupported"/> is true). Feature parity with geocachingtoolbox.com plus
/// full punctuation/accents, audio, visual playback and copy — see <see cref="MorseCodec"/>.
/// </summary>
[Tool("MorseCode", ToolCategory.Alphabets,
      Introduced = "2026-05-31", Updated = "2026-05-31",
      Keywords = ["morse", "code", "kód", "telegraph", "abeceda", "tečka", "čárka", "dot", "dash"])]
public sealed partial class MorseCodeViewModel : ToolViewModelBase, IToolTextInput, IToolTextOutput
{
    private const int ToneFrequencyHz = 600;

    private readonly MorseCodec _codec = new();
    private readonly IClipboardService _clipboard;
    private readonly IShareService _share;
    private readonly IMorseAudioService _audio;
    private readonly IDisplayRequestManager _displayRequest;

    private CancellationTokenSource? _playbackCts;
    private bool _suppressConvert;

    public MorseCodeViewModel(
        ICatalogService catalog,
        IRecentsService recents,
        IFavoriteToolsService favorites,
        IStringLocalizer localizer,
        IClipboardService clipboard,
        IShareService share,
        IMorseAudioService audio,
        IDisplayRequestManager displayRequest)
        : base("MorseCode", catalog, recents, favorites, localizer)
    {
        _clipboard = clipboard;
        _share = share;
        _audio = audio;
        _displayRequest = displayRequest;
        IsAudioSupported = audio.IsSupported;
    }

    /// <summary>0 = text → Morse, 1 = Morse → text.</summary>
    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasOutput { get; set; }

    /// <summary><see langword="true"/> when the result contains an unmapped character/code placeholder.</summary>
    [ObservableProperty]
    public partial bool HasUnknown { get; set; }

    [ObservableProperty]
    public partial int WordsPerMinute { get; set; } = 15;

    [ObservableProperty]
    public partial bool IsAudioSupported { get; set; }

    [ObservableProperty]
    public partial bool PlayAudio { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool IsFlashOn { get; set; }

    public int MinWordsPerMinute => MorseTiming.MinWordsPerMinute;

    public int MaxWordsPerMinute => MorseTiming.MaxWordsPerMinute;

    private bool IsTextToMorse => DirectionIndex != 1;

    private string CurrentMorse => IsTextToMorse ? OutputText : InputText;

    private bool CanPlay => !IsPlaying && (CurrentMorse.Contains('.') || CurrentMorse.Contains('-'));

    partial void OnInputTextChanged(string value)
    {
        if (!_suppressConvert)
        {
            Convert();
        }
    }

    partial void OnDirectionIndexChanged(int value)
    {
        // Ignore re-entrant carries and the transient -1 a RadioButtons control can emit.
        if (_suppressConvert || value is not (0 or 1))
        {
            return;
        }

        // Switching direction carries the previous result into the input, so a round-trip is one tap.
        _suppressConvert = true;
        InputText = OutputText;
        _suppressConvert = false;
        Convert();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        PlayCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnHasOutputChanged(bool value)
    {
        CopyOutputCommand.NotifyCanExecuteChanged();
        ShareOutputCommand.NotifyCanExecuteChanged();
    }

    private void Convert()
    {
        OutputText = IsTextToMorse ? _codec.Encode(InputText) : _codec.Decode(InputText);
        HasOutput = !string.IsNullOrEmpty(OutputText);
        HasUnknown = OutputText.Contains('#');
        PlayCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private void CopyOutput() => _clipboard.SetText(OutputText);

    [RelayCommand(CanExecute = nameof(HasOutput))]
    private async Task ShareOutputAsync()
    {
        try
        {
            await _share.ShareTextAsync(ToolName, OutputText);
        }
        catch (Exception)
        {
            // Sharing is best-effort; a platform share failure must not crash the tool.
        }
    }

    [RelayCommand]
    private void Clear() => InputText = string.Empty;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        StopPlaybackInternal();

        var timeline = MorseTimeline.Build(CurrentMorse);
        if (timeline.Count == 0)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _playbackCts = cts;
        IsPlaying = true;

        // Keep the screen awake for the duration: on the heads without audio the flash is the only output.
        var wakeLock = _displayRequest.RequestActive();
        var audioTask = Task.CompletedTask;
        try
        {
            var unitMs = MorseTiming.UnitMilliseconds(WordsPerMinute);

            if (IsAudioSupported && PlayAudio)
            {
                // Rendering a full WAV is CPU/allocation heavy for long input; keep it off the UI thread.
                var wav = await Task.Run(() => MorseAudioRenderer.RenderWav(timeline, ToneFrequencyHz, unitMs), cts.Token);
                audioTask = _audio.PlayAsync(wav, cts.Token);
            }

            await RunFlashAsync(timeline, unitMs, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Stopped by the user — expected.
        }
        finally
        {
            // Always observe the audio task (it shares the same cancellation) so a cancelled or failed
            // beep never surfaces as an unobserved task exception.
            try
            {
                await audioTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Audio glitches must not break the tool.
            }

            wakeLock.Dispose();
            IsFlashOn = false;
            IsPlaying = false;
            if (ReferenceEquals(_playbackCts, cts))
            {
                _playbackCts = null;
            }

            cts.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(IsPlaying))]
    private void Stop() => StopPlaybackInternal();

    public override void OnNavigatedFrom()
    {
        base.OnNavigatedFrom();
        StopPlaybackInternal();
    }

    private void StopPlaybackInternal()
    {
        _audio.Stop();
        var cts = _playbackCts;
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already finished.
            }
        }
    }

    private async Task RunFlashAsync(IReadOnlyList<MorseSignal> timeline, int unitMs, CancellationToken cancellationToken)
    {
        // Drive the flash off a monotonic baseline so timer jitter (Task.Delay overshoot) does not
        // accumulate and let the lamp drift away from the sample-accurate audio over a long message.
        var stopwatch = Stopwatch.StartNew();
        long targetMs = 0;
        try
        {
            foreach (var signal in timeline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IsFlashOn = signal.On;
                targetMs += (long)signal.Units * unitMs;
                var remaining = targetMs - stopwatch.ElapsedMilliseconds;
                if (remaining > 0)
                {
                    await Task.Delay((int)remaining, cancellationToken);
                }
            }
        }
        finally
        {
            IsFlashOn = false;
        }
    }
}
