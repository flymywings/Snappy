using System.Text.Json;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Snappy.Windows;

internal static class SmokeTest
{
    internal static void Run(App app, string reportPath)
    {
        app.Dispatcher.BeginInvoke(async () =>
        {
            var checks = new List<object>();
            var report = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(report)!);
            var owned = new List<Window>();
            void Check(string name, bool passed, string? detail = null)
            {
                checks.Add(new { name, passed, detail });
                if (!passed) throw new InvalidOperationException(name + ": " + detail);
            }
            var exit = 0;
            try
            {
                var displays = Monitors.All(); Check("Monitor discovery", displays.Count > 0);
                CaptureIconProof(Path.Combine(Path.GetDirectoryName(report)!, "icon-size-proof.png"));
                foreach (var dimensions in new[] { (1, 1), (1, 2), (2, 1), (6, 6), (3, 3), (3, 4), (4, 3) })
                {
                    var guideGrid = new GridSurface(SimpleGrids.Uniform(Guid.NewGuid(), "Guide check", dimensions.Item1, dimensions.Item2))
                    { Width = 240, Height = 240, ShowZoneLabels = false, TransparentBackground = true };
                    guideGrid.Measure(new Size(240, 240)); guideGrid.Arrange(new Rect(0, 0, 240, 240));
                    var guideImage = new RenderTargetBitmap(240, 240, 96, 96, PixelFormats.Pbgra32); guideImage.Render(guideGrid);
                    byte AlphaAt(int x, int y) { var pixel = new byte[4]; guideImage.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0); return pixel[3]; }
                    int VerticalWidth(int x) => Enumerable.Range(x - 3, 7).Count(p => AlphaAt(p, 7) == 255);
                    int HorizontalWidth(int y) => Enumerable.Range(y - 3, 7).Count(p => AlphaAt(7, p) == 255);
                    Check($"{dimensions.Item1}x{dimensions.Item2} grid backing remains transparent", AlphaAt(7, 7) == 0);
                    Check($"{dimensions.Item1}x{dimensions.Item2} vertical midpoint parity", VerticalWidth(120) == (dimensions.Item1 % 2 == 0 ? 2 : 0));
                    Check($"{dimensions.Item1}x{dimensions.Item2} horizontal midpoint parity", HorizontalWidth(120) == (dimensions.Item2 % 2 == 0 ? 2 : 0));
                    if (dimensions.Item1 >= 3) Check($"{dimensions.Item1} columns keep ordinary guides thin", VerticalWidth(240 / dimensions.Item1) == 1);
                    if (dimensions.Item2 >= 3) Check($"{dimensions.Item2} rows keep ordinary guides thin", HorizontalWidth(240 / dimensions.Item2) == 1);
                    SaveBitmap(guideImage, Path.Combine(Path.GetDirectoryName(report)!, $"grid-{dimensions.Item1}x{dimensions.Item2}.png"));
                }
                var window = new Window { Title = "Snappy verification window", Width = 500, Height = 350, MinWidth = 220, MinHeight = 150, Content = Ui.Text("Snappy native placement verification", 20) };
                owned.Add(window); window.Show(); window.Activate(); await Task.Delay(250);
                var handle = new WindowInteropHelper(window).Handle;
                var initiallyFocused = Native.GetForegroundWindow() == handle;
                var display = Monitors.ForWindow(handle); var layout = Layouts.Presets()[0];
                var staleDisplay = display with { WorkArea = display.WorkArea with { Height = display.WorkArea.Height - 40 }, Scale = display.Scale + 0.25 };
                var currentRegion = layout.Zones[0].Region;
                var refreshedBounds = Monitors.PlacementBounds(layout, currentRegion, staleDisplay);
                Check("Placement refreshes stale work area before snapping", refreshedBounds == LayoutEngine.Bounds(layout, currentRegion, display.WorkArea, display.Scale));
                var gapLayout = layout with { Gap = 8 };
                Check("Placement uses current DPI for gaps", Monitors.PlacementBounds(gapLayout, currentRegion, staleDisplay) ==
                    LayoutEngine.Bounds(gapLayout, currentRegion, display.WorkArea, display.Scale));
                var negativeDisplay = display with { WorkArea = new(-1200, -300, 1200, 1900), Orientation = "Portrait" };
                Check("Placement refresh retains negative monitor coordinates", Monitors.PlacementBounds(layout, currentRegion,
                    negativeDisplay with { WorkArea = new(-1200, -300, 1200, 1800) }, [negativeDisplay]) ==
                    LayoutEngine.Bounds(layout, currentRegion, negativeDisplay.WorkArea, negativeDisplay.Scale));
                bool RejectDisplay(Display selected, IReadOnlyList<Display> current)
                {
                    try { Monitors.PlacementBounds(layout, currentRegion, selected, current); return false; }
                    catch (InvalidOperationException) { return true; }
                }
                Check("Disconnected monitor cannot redirect placement", RejectDisplay(display, [display with { Identity = "Other monitor" }]));
                Check("Changed orientation cannot reuse stale layout", RejectDisplay(display, [display with { Orientation = "Other orientation" }]));
                var system = new WindowSystem { AllowOwnWindows = true }; var placement = new PlacementService(system);
                var target = refreshedBounds;
                var result = await placement.PlaceAsync(handle, target); await Task.Delay(150);
                Check("Native top-half placement", result.Success, result.Error);
                var actual = system.Geometry(handle).Visible;
                Check("Visible frame matches target", Near(actual, target), $"actual={actual}; target={target}");
                if (initiallyFocused) Check("Target focus restored", Native.GetForegroundWindow() == handle);
                else checks.Add(new { name = "Target focus restored", passed = (bool?)null, detail = "Skipped: this tool-launched process did not obtain foreground permission. Check through a user-triggered picker/hotkey." });
                result = await placement.PlaceAsync(handle, new PixelRect(target.X, target.Y, 20, 20));
                Check("Minimum size rejected", !result.Success && result.Error!.Contains("minimum"), result.Error);
                Check("Own overlays excluded from normal placement", !WindowSystem.Eligible(handle));
                window.WindowState = WindowState.Maximized; await Task.Delay(200);
                result = await placement.PlaceAsync(handle, target); await Task.Delay(150);
                Check("Maximized window restored and placed", result.Success && !Native.IsZoomed(handle), result.Error);
                using (var host = new HotkeyHost())
                using (var conflict = new HotkeyHost())
                {
                    var keys = new Dictionary<string, Hotkey> { ["test"] = new(3, 0x86) }; // Ctrl+Alt+F23
                    Check("Native hotkey registration", host.Registry.TryApply(keys, out var error), error);
                    Check("Native hotkey conflict rejected", !conflict.Registry.TryApply(keys, out _));
                }
                Native.KeyboardProc keyboard = (code, message, data) => Native.CallNextHookEx(0, code, message, data);
                var keyboardHook = Native.SetWindowsHookEx(13, keyboard, Native.GetModuleHandle(null), 0);
                Check("Drag cancellation keyboard hook available", keyboardHook != 0);
                Native.UnhookWindowsHookEx(keyboardHook); GC.KeepAlive(keyboard);
                var foregroundBeforeOverlay = Native.GetForegroundWindow();
                var overlay = new ZoneOverlay(); owned.Add(overlay); overlay.Display(display, layout, layout.Zones[0].Id); await Task.Delay(100);
                var style = Native.GetWindowLongPtr(new WindowInteropHelper(overlay).Handle, Native.GwlExStyle).ToInt64();
                Check("Overlay is click-through and non-activating", (style & 0x08000020) == 0x08000020 && Native.GetForegroundWindow() == foregroundBeforeOverlay);
                Capture(overlay, Path.Combine(Path.GetDirectoryName(report)!, "overlay-preview.png")); overlay.Close(); owned.Remove(overlay);
                var portrait = display with { Orientation = "Portrait", WorkArea = new(0, 0, 1080, 1920) };
                var horizontal = new System.Windows.Controls.Primitives.ScrollBar
                { Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 100, Value = 50, ViewportSize = 10, LargeChange = 10, Width = 220, Height = 10 };
                var scrollFixture = new Window { Width = 280, Height = 120, Content = horizontal }; owned.Add(scrollFixture); scrollFixture.Show(); await Task.Delay(50);
                var track = (System.Windows.Controls.Primitives.Track)horizontal.Template.FindName("PART_Track", horizontal);
                Check("Themed horizontal thumb moves values to the right", track.ValueFromDistance(20, 0) > 0);
                var pageRight = (RoutedCommand)track.IncreaseRepeatButton.Command;
                pageRight.Execute(null, horizontal);
                Check("Themed horizontal paging moves right", horizontal.Value > 50);
                scrollFixture.Close(); owned.Remove(scrollFixture);
                var picker = new PickerWindow(layout, portrait, _ => { }); owned.Add(picker); picker.Show(); await Task.Delay(150);
                Capture(picker, Path.Combine(Path.GetDirectoryName(report)!, "picker-preview.png")); picker.Close(); owned.Remove(picker);
                var editor = new EditorWindow(Layouts.Defaults(), _ => null); owned.Add(editor); editor.Show(); await Task.Delay(150);
                var layoutName = Descendants(editor).OfType<TextBox>().First(t => t.Text == "Left / right");
                var editorGrid = Descendants(editor).OfType<GridSurface>().Single();
                var editRegion = new GridRegion(0, 0, 1, 1); editorGrid.Select(editRegion);
                editorGrid.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseMoveEvent });
                Check("Editor keeps chosen region while pointer leaves selection", editorGrid.Selection == editRegion);
                var layoutSelector = Descendants(editor).OfType<ComboBox>().Single(c => c.DisplayMemberPath == "Name");
                Check("Themed layout selector shows readable name", Descendants(layoutSelector).OfType<TextBlock>().Any(t => t.Text == "Left / right"));
                layoutName.Text = "Unsaved monitor draft"; editor.RefreshDisplays();
                Check("Display refresh keeps unsaved editor fields", layoutName.Text == "Unsaved monitor draft");
                layoutName.Text = "Left / right";
                Capture(editor, Path.Combine(Path.GetDirectoryName(report)!, "editor-preview.png"));
                Check("Picker and editor render", File.Exists(Path.Combine(Path.GetDirectoryName(report)!, "picker-preview.png")));
                var fixedWindow = new Window { Width = 300, Height = 200, ResizeMode = ResizeMode.NoResize }; owned.Add(fixedWindow); fixedWindow.Show();
                Check("Non-resizable window rejected", !WindowSystem.Eligible(new WindowInteropHelper(fixedWindow).Handle, true));
                var simpleSettings = Layouts.Defaults();
                var panel = new PanelWindow(d => LayoutEngine.Resolve(simpleSettings, d.Identity, d.Orientation),
                    (d, c, r) => { SimpleGrids.Configure(simpleSettings, d.Identity, d.Orientation, c, r); return null; }, () => { }, _ => { }, null);
                owned.Add(panel);
                var foregroundBeforePanel = Native.GetForegroundWindow(); panel.Show(); await Task.Delay(100);
                panel.UpdateTarget(handle, "Window A", display);
                var panelHandle = new WindowInteropHelper(panel).Handle;
                Check("Main panel uses native per-pixel composition", (Native.GetWindowLongPtr(panelHandle, Native.GwlExStyle).ToInt64() & 0x80000) != 0);
                Native.SendMessageTimeout(panelHandle, 0x21, handle, 0, 2, 150, out var mouseActivation);
                Check("Panel mouse clicks do not activate", mouseActivation == 3 && Native.GetForegroundWindow() == foregroundBeforePanel);
                int Hit(Point point)
                {
                    var packed = new nint(unchecked((int)((uint)(ushort)(int)point.X | (uint)(ushort)(int)point.Y << 16)));
                    Native.SendMessageTimeout(panelHandle, 0x84, 0, packed, 2, 150, out var hit);
                    return (int)hit;
                }
                Check("Custom caption retains native window dragging", Hit(panel.PointToScreen(new Point(120, 23))) == 2);
                var gearButton = Descendants(panel).OfType<Button>().Single(b => b.ToolTip as string == "Advanced settings");
                Check("Caption settings button stays interactive", Hit(gearButton.PointToScreen(new Point(gearButton.ActualWidth / 2, gearButton.ActualHeight / 2))) == 1);
                var monitorSelector = Descendants(panel).OfType<ComboBox>().Single();
                Check("Themed monitor label is compact and readable", Descendants(monitorSelector).OfType<TextBlock>().Any(t => t.Text == display.Label));
                var beforePopup = Native.GetForegroundWindow(); monitorSelector.IsDropDownOpen = true; await Task.Delay(80);
                Check("Themed monitor popup opens without activating panel", monitorSelector.IsDropDownOpen && Native.GetForegroundWindow() == beforePopup);
                var popup = (System.Windows.Controls.Primitives.Popup)monitorSelector.Template.FindName("PART_Popup", monitorSelector);
                var popupImage = Render((FrameworkElement)popup.Child);
                Check("Monitor popup background stays fully opaque", Pixel(popupImage, 5, 10)[3] == 255);
                monitorSelector.IsDropDownOpen = false;
                using (var iconSource = Application.GetResourceStream(new Uri("pack://application:,,,/Snappy;component/Assets/Snappy.ico"))!.Stream)
                using (var appIcon = new System.Drawing.Icon(iconSource, new System.Drawing.Size(16, 16)))
                    Check("Embedded tray icon decodes at native 16px", appIcon.Width == 16 && appIcon.Height == 16);
                var grid = Descendants(panel).OfType<GridSurface>().Single();
                var panelImage = Render(panel);
                var gridPoint = grid.TransformToAncestor(panel).Transform(new Point(7, 7));
                var gridAlpha = Pixel(panelImage, (int)gridPoint.X, (int)gridPoint.Y)[3];
                Check("Panel and grid share one 90-percent backdrop", gridAlpha is >= 229 and <= 231);
                var midpointPoint = grid.TransformToAncestor(panel).Transform(new Point(grid.ActualWidth / 2, 7));
                Check("Main midpoint guide remains fully opaque", Pixel(panelImage, (int)midpointPoint.X, (int)midpointPoint.Y)[3] == 255);
                var titleBlock = Descendants(panel).OfType<TextBlock>().First(t => t.Text == "Snappy");
                var titleOrigin = titleBlock.TransformToAncestor(panel).Transform(new Point(0, 0));
                var opaqueText = false;
                for (var y = 0; y < (int)titleBlock.ActualHeight && !opaqueText; y++)
                for (var x = 0; x < (int)titleBlock.ActualWidth && !opaqueText; x++)
                {
                    var pixel = Pixel(panelImage, (int)titleOrigin.X + x, (int)titleOrigin.Y + y);
                    opaqueText = pixel[3] == 255 && pixel[0] > 220 && pixel[1] > 220 && pixel[2] > 220;
                }
                Check("Caption glyphs remain opaque over translucent backdrop", opaqueText);
                var priorLayout = grid.Layout; var staleSelection = false;
                void ObserveSelection(GridRegion region) => staleSelection = true;
                grid.Selected += ObserveSelection; grid.BeginSelection(new Point(grid.ActualWidth - 5, 5));
                grid.Layout = SimpleGrids.Uniform(Guid.NewGuid(), "New monitor grid", 1, 2); grid.CompleteSelection(new Point(5, 5));
                Check("Replacing grid cancels stale selection from prior layout", !staleSelection);
                grid.Selected -= ObserveSelection; grid.Layout = priorLayout;
                var lastSnap = new TaskCompletionSource<PlacementResult>(); nint requestedWindow = 0; GridRegion? requestedRegion = null;
                panel.RefreshDisplays([staleDisplay]);
                panel.SnapRequested += async (win, selectedLayout, region, selectedDisplay) =>
                {
                    requestedWindow = win; requestedRegion = region;
                    var placementResult = await placement.PlaceAsync(win, Monitors.PlacementBounds(selectedLayout, region, selectedDisplay));
                    lastSnap.TrySetResult(placementResult);
                };
                grid.BeginSelection(new Point(10, 10)); grid.CompleteSelection(new Point(10, 10));
                Check("Single cell click places Window A and keeps panel open", (await lastSnap.Task).Success && requestedWindow == handle && panel.IsVisible && requestedRegion == new GridRegion(0, 0, 1, 1));
                Check("Grid click uses live bounds despite stale monitor cache", Near(system.Geometry(handle).Visible,
                    LayoutEngine.Bounds(panel.CurrentLayout, requestedRegion!, display.WorkArea, display.Scale)));
                var second = new Window { Title = "Snappy verification Window B", Width = 500, Height = 300 }; owned.Add(second); second.Show(); await Task.Delay(100);
                var secondHandle = new WindowInteropHelper(second).Handle;
                panel.UpdateTarget(secondHandle, "Window B", display); lastSnap = new();
                grid.BeginSelection(new Point(5, 5)); grid.CompleteSelection(new Point(grid.ActualWidth - 5, grid.ActualHeight - 5));
                Check("Multi-cell selection places Window B without reopening", (await lastSnap.Task).Success && requestedWindow == secondHandle && panel.IsVisible && requestedRegion == new GridRegion(0, 0, 2, 1));
                var requestsBeforePause = requestedWindow; panel.SetPaused(true); lastSnap = new();
                grid.BeginSelection(new Point(5, 5)); grid.CompleteSelection(new Point(5, 5));
                Check("Paused panel rejects snapping", !lastSnap.Task.IsCompleted && requestedWindow == requestsBeforePause && !grid.IsEnabled);
                panel.SetPaused(false); panel.UpdateTarget(0, "Select a resizable app window", null);
                Check("Missing target disables panel grid", !grid.IsEnabled && panel.Target == 0);
                var beforeGridChange = simpleSettings.Layouts.Count;
                var increaseRows = Descendants(panel).OfType<Button>().Last(b => b.Content as string == "+");
                increaseRows.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check("Row stepper changes and saves active layout immediately", panel.CurrentLayout.Rows == 2 && simpleSettings.Layouts.Count == beforeGridChange + 1);
                panel.UpdateTarget(secondHandle, "Window B", display);
                panel.ShowStatus("");
                Capture(panel, Path.Combine(Path.GetDirectoryName(report)!, "panel-preview.png"));
                foreach (var background in new[] { "light", "dark", "busy" })
                    CaptureBackdrop(panel, background, Path.Combine(Path.GetDirectoryName(report)!, $"panel-on-{background}.png"));
                grid.Select(new GridRegion(0, 0, 1, 1));
                Capture(panel, Path.Combine(Path.GetDirectoryName(report)!, "panel-selected-preview.png")); grid.Select(null);
                panel.Close(); Check("Panel close hides instead of exiting", !panel.IsVisible); panel.Show();
                Check("Panel can be revealed again", panel.IsVisible); panel.Exit(); owned.Remove(panel);
                var failurePanel = new PanelWindow(d => LayoutEngine.Resolve(simpleSettings, d.Identity, d.Orientation),
                    (_, _, _) => "Could not save grid: disk unavailable.", () => { }, _ => { }, null);
                owned.Add(failurePanel); failurePanel.Show(); await Task.Delay(50);
                var previousColumns = failurePanel.CurrentLayout.Columns;
                var columnPlus = Descendants(failurePanel).OfType<Button>().First(b => b.Content as string == "+");
                columnPlus.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check("Failed grid save retains working grid and reports inline", failurePanel.CurrentLayout.Columns == previousColumns &&
                    Descendants(failurePanel).OfType<TextBlock>().Any(t => t.Text.Contains("disk unavailable")));
                var portraitFixture = display with { Identity = "Synthetic portrait", Orientation = "Portrait" };
                failurePanel.RefreshDisplays([display, portraitFixture]);
                failurePanel.UpdateTarget(secondHandle, "Window B", display);
                failurePanel.UpdateTarget(secondHandle, "Window B", portraitFixture);
                Check("Same target crossing monitors refreshes grid geometry", failurePanel.CurrentLayout.Columns == 1 && failurePanel.CurrentLayout.Rows == 2);
                failurePanel.Exit(); owned.Remove(failurePanel);
                var revealName = "Snappy self-test " + Guid.NewGuid();
                using (var revealHost = new HotkeyHost(revealName))
                {
                    var revealed = false; revealHost.RevealRequested += () => revealed = true;
                    Check("Relaunch request reaches existing process host", HotkeyHost.RevealExisting(revealName));
                    await Task.Delay(100); Check("Relaunch request invokes panel reveal", revealed);
                }
                var ready = new TaskCompletionSource<(nint Handle, System.Windows.Threading.Dispatcher Dispatcher)>();
                var block = 0;
                var worker = new Thread(() =>
                {
                    using var source = new HwndSource(new HwndSourceParameters("Snappy unresponsive-window test") { Width = 400, Height = 300, WindowStyle = 0x10CF0000 });
                    source.AddHook((nint hwnd, int message, nint wp, nint lp, ref bool handled) =>
                    { if (message == 0x24 && Volatile.Read(ref block) == 1) Thread.Sleep(800); return 0; });
                    ready.SetResult((source.Handle, System.Windows.Threading.Dispatcher.CurrentDispatcher));
                    System.Windows.Threading.Dispatcher.Run();
                }) { IsBackground = true };
                worker.SetApartmentState(ApartmentState.STA); worker.Start();
                var foreign = await ready.Task; Volatile.Write(ref block, 1);
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    var unresponsive = await placement.PlaceAsync(foreign.Handle, target);
                    Check("Unresponsive size query fails within bounded time", !unresponsive.Success && elapsed.ElapsedMilliseconds < 650,
                        $"{elapsed.ElapsedMilliseconds} ms; {unresponsive.Error}");
                }
                finally
                {
                    Volatile.Write(ref block, 0);
                    foreign.Dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Send);
                    await Task.Run(() => worker.Join(2000));
                }
            }
            catch (Exception ex) { checks.Add(new { name = "Failure", passed = false, detail = ex.ToString() }); exit = 1; }
            finally
            {
                foreach (var window in owned) window.Close();
                File.WriteAllText(report, JsonSerializer.Serialize(new { passed = exit == 0, os = Environment.OSVersion.ToString(), checks }, new JsonSerializerOptions { WriteIndented = true }));
                app.Shutdown(exit);
            }
        });
    }
    private static bool Near(PixelRect a, PixelRect b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1 && Math.Abs(a.Width - b.Width) <= 1 && Math.Abs(a.Height - b.Height) <= 1;
    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i); yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    private static void Capture(Window window, string path)
    {
        SaveBitmap(Render(window), path);
    }
    private static RenderTargetBitmap Render(FrameworkElement element)
    {
        element.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        image.Render(element); return image;
    }
    private static byte[] Pixel(BitmapSource image, int x, int y)
    {
        var pixel = new byte[4]; image.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0); return pixel;
    }
    private static void SaveBitmap(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void CaptureBackdrop(Window window, string background, string path)
    {
        var image = Render(window); var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var light = background == "light";
            dc.DrawRectangle(new SolidColorBrush(light ? Color.FromRgb(242, 244, 247) : Color.FromRgb(16, 18, 22)), null, new Rect(0, 0, image.PixelWidth + 32, image.PixelHeight + 32));
            if (background == "busy")
                for (var row = 0; row < image.PixelHeight + 32; row += 20)
                for (var column = 0; column < image.PixelWidth + 32; column += 20)
                    dc.DrawRectangle(new SolidColorBrush((row + column) % 40 == 0 ? Color.FromRgb(222, 181, 137) : Color.FromRgb(52, 98, 130)), null, new Rect(column, row, 20, 20));
            dc.DrawImage(image, new Rect(16, 16, image.PixelWidth, image.PixelHeight));
        }
        var composite = new RenderTargetBitmap(image.PixelWidth + 32, image.PixelHeight + 32, 96, 96, PixelFormats.Pbgra32); composite.Render(visual); SaveBitmap(composite, path);
    }
    private static void CaptureIconProof(string path)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(28, 31, 35)), null, new Rect(0, 0, 330, 480));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(242, 244, 247)), null, new Rect(330, 0, 330, 480));
            var sizes = new[] { 16, 24, 32, 256 }; var positions = new[] { 36, 82, 130, 204 };
            for (var column = 0; column < 2; column++)
            for (var row = 0; row < sizes.Length; row++)
            {
                dc.DrawImage(Ui.BrandIcon, new Rect(column * 330 + 20, positions[row], sizes[row], sizes[row]));
                var label = new System.Windows.Media.FormattedText($"{sizes[row]} px", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface(Ui.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12,
                    column == 0 ? Brushes.White : Brushes.Black, 1);
                dc.DrawText(label, new Point(column * 330 + 20, positions[row] - 20));
            }
        }
        var image = new RenderTargetBitmap(660, 480, 96, 96, PixelFormats.Pbgra32); image.Render(visual); SaveBitmap(image, path);
    }
}
