using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using System.Diagnostics;
using System.Text.Json;
namespace WirePulse;

public sealed class App : Application
{
    Settings settings = null!; readonly Spectrum spectrum = new(); MediaService media = null!; AudioCapture audio = null!; SettingsWindow? panel; Window? screenProbe; readonly List<Surface> walls = [];
    readonly Dictionary<string, Settings> profiles = [], available = []; DispatcherTimer? maintenance; TrayIcon? tray; bool polling, quitting, rebuilding, lastIndependent; DateTime lastStatus = DateTime.MinValue; double lastCpu, lastWall; double cpuPercent;
    sealed class Surface(Window window, WallpaperView view, string name, double hz, PixelRect bounds) { public Window Window = window; public WallpaperView View = view; public string Name = name; public double Hz = hz; public FramePacer Pacer = new(); public PixelRect Bounds = bounds; public bool Active = true, Scheduled, Covered; public double Target; }
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime life)
        {
            life.ShutdownMode = ShutdownMode.OnExplicitShutdown; settings = new(); media = new(settings); audio = new(spectrum); audio.Start();
            life.Exit += (_, _) => { quitting = true; maintenance?.Stop(); foreach (var wall in walls) { wall.Active = false; wall.View.Dispose(); } audio.Dispose(); tray?.Dispose(); screenProbe?.Close(); };
            var menu = new NativeMenu(); var show = new NativeMenuItem("Ajustes de WirePulse"); show.Click += (_, _) => ShowSettings(); menu.Add(show);
            var pause = new NativeMenuItem("Pausar / reanudar animación"); pause.Click += (_, _) => { settings.Paused = !settings.Paused; foreach (var p in profiles.Values) p.Paused = settings.Paused; }; menu.Add(pause);
            var exit = new NativeMenuItem("Salir"); exit.Click += (_, _) => life.Shutdown(); menu.Add(exit);
            tray = new TrayIcon { Icon = new WindowIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "WirePulse.png")), ToolTipText = "WirePulse", Menu = menu, IsVisible = true }; tray.Clicked += (_, _) => ShowSettings(); TrayIcon.SetIcons(this, new TrayIcons { tray });
            screenProbe = new Window(); screenProbe.Screens.Changed += (_, _) => { if (!quitting) Dispatcher.UIThread.Post(CreateWalls); }; lastIndependent = settings.IndependentDisplays; CreateWalls();
            settings.Changed += () => { if (lastIndependent != settings.IndependentDisplays) { lastIndependent = settings.IndependentDisplays; if (lastIndependent) foreach (var key in profiles.Keys.ToArray()) { if (!profiles[key].HasSaved) profiles[key] = new Settings(key, settings); profiles[key].Save(); } CreateWalls(); } };
            if (!Program.Args.Contains("--no-settings")) ShowSettings(); maintenance = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, Maintain); maintenance.Start(); Maintain(null, EventArgs.Empty);
            if (double.TryParse(Program.Option("--probe-seconds"), out var seconds)) { var stop = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; stop.Tick += (_, _) => { WriteStatus(); if (Program.Option("--render-output") is string output && walls.Count > 0) { var view = walls[0].View; using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)view.Bounds.Width, (int)view.Bounds.Height)); bitmap.Render(view); bitmap.Save(output, new Avalonia.Media.Imaging.PngBitmapEncoderOptions()); } life.Shutdown(); }; stop.Start(); }
        }
        base.OnFrameworkInitializationCompleted();
    }
    void ShowSettings() { if (panel == null) { panel = new SettingsWindow(settings, spectrum, media, () => audio.Status, available, () => string.Join("\n", walls.Select(w => w.Covered ? $"{w.Name}: pausado por pantalla completa" : $"{w.Name}: {w.View.Statistics.Fps:F0} FPS de dibujo / objetivo {w.Target:F0} · {w.View.Statistics.DrawMs:F2} ms · {w.View.Statistics.Backend}"))); panel.Closed += (_, _) => panel = null; } panel.Show(); panel.Activate(); }
    void CreateWalls()
    {
        if (rebuilding || quitting || screenProbe == null) return; rebuilding = true; try
        {
            foreach (var old in walls) { old.Active = false; old.Window.Close(); old.View.Dispose(); }
            walls.Clear(); available.Clear(); int index = 0;
            foreach (var screen in screenProbe.Screens.All)
            {
                string key = screen.DisplayName ?? ("Pantalla " + (++index)); if (available.ContainsKey(key)) key += " " + (++index); if (!profiles.TryGetValue(key, out var profile)) { profile = new Settings(key, settings); profiles[key] = profile; }
                available[key] = profile;
                var view = new WallpaperView(settings.IndependentDisplays ? profile : settings, spectrum, media); var window = new Window { Title = "WirePulse Desktop", WindowDecorations = WindowDecorations.None, ShowInTaskbar = false, CanResize = false, WindowStartupLocation = WindowStartupLocation.Manual, Position = screen.Bounds.Position, Width = screen.Bounds.Width / screen.Scaling, Height = screen.Bounds.Height / screen.Scaling, Background = Brushes.Black, Content = view, ShowActivated = false }; var surface = new Surface(window, view, key, DisplayTiming.RefreshRate(screen.Bounds), screen.Bounds); walls.Add(surface);
                window.Closed += (_, _) => surface.Active = false; window.Opened += (_, _) => { try { var handle = window.TryGetPlatformHandle() ?? throw new Exception("No existe ventana nativa"); DesktopHost.Attach(handle.Handle, screen.Bounds.Width, screen.Bounds.Height, screen.Bounds.X, screen.Bounds.Y); Schedule(surface); } catch (Exception e) { Console.Error.WriteLine(e); (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown(1); } }; window.Show();
            }
            panel?.RefreshDisplays();
        }
        finally { rebuilding = false; }
    }
    void Schedule(Surface surface) { if (surface.Scheduled || surface.Covered || !surface.Active) return; surface.Scheduled = true; surface.Window.RequestAnimationFrame(time => { surface.Scheduled = false; if (!surface.Active || surface.Covered || quitting) return; var s = surface.View.Configuration; double target = s.On("unlimitedfps") ? surface.Hz : Math.Min(surface.Hz, Math.Clamp(s.N("fps"), 15, 360)); if (s.RenderProfile == "Ligero") target = Math.Min(target, 60); if (s.Paused) target = Math.Min(target, 15); if (!s.On("sphere") && !s.On("spectrum")) target = 4; surface.Target = target; if (surface.Pacer.Due(time.TotalSeconds, target)) surface.View.Advance(time.TotalSeconds); Schedule(surface); }); }
    async void Maintain(object? sender, EventArgs e)
    {
        if (quitting) return; foreach (var w in walls) { w.Covered = w.View.Configuration.SuspendFullscreen && DisplayTiming.Covered(w.Bounds); if (!w.Covered) Schedule(w); if (!w.Covered) w.View.Adapt(w.Target > 0 ? w.Target : 60); }
        if (OperatingSystem.IsWindows() && walls.Any(w => !DesktopHost.Alive(w.Window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero))) CreateWalls();
        if ((DateTime.UtcNow - lastStatus).TotalSeconds >= 2) { lastStatus = DateTime.UtcNow; WriteStatus(); }
        if (polling) return; polling = true; try { await Task.Run(media.Poll); } catch (Exception error) { Trace.WriteLine(error); } finally { polling = false; }
    }
    void WriteStatus() { try { var s = spectrum.Snapshot(); using var process = Process.GetCurrentProcess(); double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, cpu = process.TotalProcessorTime.TotalSeconds; if (lastWall > 0) cpuPercent = 100 * (cpu - lastCpu) / (now - lastWall) / Environment.ProcessorCount; lastWall = now; lastCpu = cpu; var path = Program.Option("--probe-output") ?? Path.Combine(Settings.Folder, "status.json"); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); File.WriteAllText(path, JsonSerializer.Serialize(new { audio = audio.Status, rate = s.SampleRate, fft = s.FftSize, peak = s.PeakHz, frames = s.Frames, age = s.Age, bands = s.Bands, renderFrames = walls.Sum(w => w.View.Frames), provider = media.Current.Provider, title = media.Current.Title, media = media.Status, processCpuPercent = cpuPercent, workingSetMiB = process.WorkingSet64 / 1048576d, displays = walls.Select(w => new { name = w.Name, refreshHz = w.Hz, targetFps = w.Target, renderedFps = w.View.Statistics.Fps, drawMs = w.View.Statistics.DrawMs, backend = w.View.Statistics.Backend, profile = w.View.Configuration.RenderProfile, suspendedFullscreen = w.Covered }) }, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
}
