using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class ApplicationsView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _robinLog = new();
    private readonly Control[] _panels;
    private readonly Control[] _canvases;

    // Adder state & timer
    private readonly DispatcherTimer _adderTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private List<LongNumberAdder.AdditionStep> _adderSteps = new();
    private int _adderStepIdx = -1;
    private string _adderResultStr = "";

    // Editor state
    private readonly UndoRedoEditor _editor = new();
    private bool _demoRunning;

    // Scheduler state & timer
    private readonly DispatcherTimer _robinTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private RoundRobinScheduler.SchedulerReport? _schedulerReport;
    private int _schedulerSliceIdx = -1;

    private CancellationTokenSource? _tabTransition;

    public ApplicationsView()
    {
        InitializeComponent();

        _panels = [PanelAdder, PanelEditor, PanelRobin];
        _canvases = [AdderCanvas, EditorCanvas, TimelineCanvas];

        RobinProcessList.ItemsSource = _robinLog;

        _adderTimer.Tick += (_, _) => OnAdderTick();
        _robinTimer.Tick += (_, _) => OnRobinTick();

        // Tab switcher
        TabAdderBtn.Click += (_, _) => SwitchTab(0);
        TabEditorBtn.Click += (_, _) => SwitchTab(1);
        TabRobinBtn.Click += (_, _) => SwitchTab(2);

        // Adder controls
        AdderRunBtn.Click += (_, _) => ToggleAdderPlay();
        AdderStepBtn.Click += (_, _) => StepAdder();
        AdderResetBtn.Click += (_, _) => ResetAdder();
        AdderPresetNinesBtn.Click += (_, _) => SetAdderPreset("99999999999999999999999999", "1");
        AdderPresetHugeBtn.Click += (_, _) => SetAdderPreset("123456789012345678901234567890", "987654321098765432109876543210");

        // Editor controls
        EditorTypeBtn.Click += (_, _) => EditorType();
        EditorDeleteBtn.Click += (_, _) => EditorDelete();
        EditorUndoBtn.Click += (_, _) => EditorUndo();
        EditorRedoBtn.Click += (_, _) => EditorRedo();
        EditorClearBtn.Click += (_, _) => EditorClear();
        EditorPresetHelloBtn.Click += (_, _) => RunHelloPreset();
        EditorPresetTypingBtn.Click += (_, _) => _ = RunTypingDemoAsync();

        // Scheduler controls: ONLY update label on slider change, DO NOT auto-run
        RobinQuantumSlider.ValueChanged += (_, _) =>
        {
            RobinQuantumLabel.Text = ((int)RobinQuantumSlider.Value).ToString();
        };
        RobinRunBtn.Click += (_, _) => ToggleRobinPlay();
        RobinStepBtn.Click += (_, _) => StepScheduler();
        RobinResetBtn.Click += (_, _) => ResetScheduler();

        // Initial setup
        ResetAdder();
        UpdateEditorUI();
        ResetScheduler(); // Starts clean and empty as requested
    }

    private void SwitchTab(int index)
    {
        _adderTimer.Stop();
        AdderRunBtn.Content = "Старт (Авто)";
        _robinTimer.Stop();
        RobinRunBtn.Content = "Запустить симуляцию";

        TabAdderBtn.Classes.Set("accent", index == 0);
        TabEditorBtn.Classes.Set("accent", index == 1);
        TabRobinBtn.Classes.Set("accent", index == 2);

        var targetPanel = _panels[index];
        var targetCanvas = _canvases[index];
        if (targetPanel.IsVisible && targetCanvas.IsVisible)
            return;

        _tabTransition?.Cancel();
        _tabTransition?.Dispose();
        var transition = new CancellationTokenSource();
        _tabTransition = transition;

        _ = AnimateTabChangeAsync(targetPanel, targetCanvas, transition.Token);
    }

    private async Task AnimateTabChangeAsync(
        Control targetPanel,
        Control targetCanvas,
        CancellationToken cancellationToken)
    {
        try
        {
            // The old tab is replaced at once (like any tab control); the new content then fades and slides
            // in, which is fast to react to and still reads as motion.
            foreach (var panel in _panels)
            {
                panel.IsVisible = panel == targetPanel;
                panel.Opacity = 1;
                panel.RenderTransform = null;
            }

            foreach (var canvas in _canvases)
            {
                canvas.IsVisible = canvas == targetCanvas;
                canvas.Opacity = 1;
                canvas.RenderTransform = null;
            }

            if (ReferenceEquals(targetCanvas, EditorCanvas))
                SyncEditorCanvas();

            await Task.WhenAll(
                Motion.FadeSlideInAsync(targetPanel, 14, Motion.Normal, cancellationToken),
                Motion.FadeSlideInAsync(targetCanvas, 14, Motion.Normal, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // A newer tab click owns the final state.
        }
        catch (Exception exception)
        {
            // A failed transition must still leave a usable page; otherwise log nothing and show the tab.
            Console.Error.WriteLine($"Переключение вкладки: {exception.Message}");
            foreach (var panel in _panels)
            {
                panel.IsVisible = panel == targetPanel;
                panel.Opacity = 1;
                panel.RenderTransform = null;
            }

            foreach (var canvas in _canvases)
            {
                canvas.IsVisible = canvas == targetCanvas;
                canvas.Opacity = 1;
                canvas.RenderTransform = null;
            }
        }
    }

    public bool HandleShortcut(Key key)
    {
        if (PanelAdder.IsVisible)
        {
            if (key == Key.Space) { ToggleAdderPlay(); return true; }
            if (key == Key.Right) { StepAdder(); return true; }
            if (key == Key.R) { ResetAdder(); return true; }
        }
        else if (PanelRobin.IsVisible)
        {
            if (key == Key.Space) { ToggleRobinPlay(); return true; }
            if (key == Key.Right) { StepScheduler(); return true; }
            if (key == Key.R) { ResetScheduler(); return true; }
        }
        return false;
    }

    /// <summary>Ctrl+Z on the applications page: only meaningful while the editor tab is open.</summary>
    public bool HandleEditorUndo()
    {
        if (!PanelEditor.IsVisible)
            return false;

        EditorUndo();
        return true;
    }

    /// <summary>Ctrl+Y on the applications page: only meaningful while the editor tab is open.</summary>
    public bool HandleEditorRedo()
    {
        if (!PanelEditor.IsVisible)
            return false;

        EditorRedo();
        return true;
    }

    #region Adder
    private void PrepareAdder()
    {
        string a = AdderNumAInput.Text?.Trim() ?? "0";
        string b = AdderNumBInput.Text?.Trim() ?? "0";
        var (res, steps) = LongNumberAdder.AddWithSteps(a, b);
        _adderSteps = steps;
        _adderResultStr = res;
        _adderStepIdx = -1;
    }

    private void ResetAdder()
    {
        _adderTimer.Stop();
        AdderRunBtn.Content = "Старт (Авто)";
        PrepareAdder();

        string a = AdderNumAInput.Text?.Trim() ?? "0";
        string b = AdderNumBInput.Text?.Trim() ?? "0";
        AdderCanvas.SetData(a, b, _adderResultStr, _adderSteps, -1);
        AdderResultOutput.Text = "Нажмите «Старт» для пошаговой анимации сложения";

        StatAdderDigitsA.Value = a.Length.ToString();
        StatAdderDigitsB.Value = b.Length.ToString();
        StatAdderDigitsRes.Value = _adderResultStr.Length.ToString();
        StatAdderTime.Value = "—";
    }

    private void ToggleAdderPlay()
    {
        if (_adderTimer.IsEnabled)
        {
            _adderTimer.Stop();
            AdderRunBtn.Content = "Продолжить";
            return;
        }

        if (_adderSteps.Count == 0 || _adderStepIdx >= _adderSteps.Count - 1)
        {
            PrepareAdder();
        }

        _adderTimer.Start();
        AdderRunBtn.Content = "Пауза";
        OnAdderTick();
    }

    private void OnAdderTick()
    {
        StepAdder();
        if (_adderStepIdx >= _adderSteps.Count - 1)
        {
            _adderTimer.Stop();
            AdderRunBtn.Content = "Старт (Авто)";
        }
    }

    private void StepAdder()
    {
        if (_adderSteps.Count == 0)
            PrepareAdder();

        string a = AdderNumAInput.Text?.Trim() ?? "0";
        string b = AdderNumBInput.Text?.Trim() ?? "0";

        _adderStepIdx++;
        if (_adderStepIdx < _adderSteps.Count)
        {
            AdderCanvas.SetData(a, b, _adderResultStr, _adderSteps, _adderStepIdx);
            AdderResultOutput.Text = $"Разряд {_adderStepIdx + 1} из {_adderSteps.Count}: " +
                $"{_adderSteps[_adderStepIdx].DigitA} + {_adderSteps[_adderStepIdx].DigitB} + перенос({_adderSteps[_adderStepIdx].InputCarry}) = {_adderSteps[_adderStepIdx].Sum}";
        }
        else
        {
            AdderCanvas.SetData(a, b, _adderResultStr, _adderSteps, -1);
            AdderResultOutput.Text = $"Сумма: {_adderResultStr}";
        }
    }

    private void SetAdderPreset(string a, string b)
    {
        AdderNumAInput.Text = a;
        AdderNumBInput.Text = b;
        ResetAdder();
    }
    #endregion

    #region Editor
    private void EditorType()
    {
        string txt = EditorTypeInput.Text ?? "";
        if (!string.IsNullOrEmpty(txt))
        {
            _editor.Type(txt);
            UpdateEditorUI();
        }
    }

    private void EditorDelete()
    {
        int count = (int)(EditorDeleteCount.Value ?? 1);
        _editor.Delete(count);
        UpdateEditorUI();
    }

    private void EditorUndo()
    {
        if (_editor.Undo())
            UpdateEditorUI();
    }

    private void EditorRedo()
    {
        if (_editor.Redo())
            UpdateEditorUI();
    }

    private void EditorClear()
    {
        _editor.Clear();
        UpdateEditorUI();
    }

    private void RunHelloPreset()
    {
        EditorTypeInput.Text = "Hello, world!";
        _editor.Clear();
        _editor.Type(EditorTypeInput.Text);
        UpdateEditorUI();
    }

    /// <summary>Types the phrase in small chunks so the push animation can be seen several times in a row.</summary>
    private async Task RunTypingDemoAsync()
    {
        if (_demoRunning)
            return;

        _demoRunning = true;
        try
        {
            _editor.Clear();
            UpdateEditorUI();
            await Task.Delay(260);

            foreach (var chunk in new[] { "Привет", ", мир", "!" })
            {
                _editor.Type(chunk);
                EditorTypeInput.Text = chunk;
                UpdateEditorUI();
                await Task.Delay(520);
            }
        }
        finally
        {
            _demoRunning = false;
        }
    }

    private void UpdateEditorUI()
    {
        EditorCurrentText.Text = _editor.Text;
        StatEditorLength.Value = _editor.Text.Length.ToString();

        var undoActions = _editor.GetUndoActions();
        var redoActions = _editor.GetRedoActions();

        StatEditorUndoCount.Value = undoActions.Count.ToString();
        StatEditorRedoCount.Value = redoActions.Count.ToString();

        // The canvas derives its push / transfer animation from the stack diff.
        EditorCanvas.DocumentText = _editor.Text;
        EditorCanvas.SetStacks(
            undoActions.Select(action => action.ToString()).ToList(),
            redoActions.Select(action => action.ToString()).ToList());
    }

    private void SyncEditorCanvas()
    {
        EditorCanvas.DocumentText = _editor.Text;
        EditorCanvas.SetStacks(
            _editor.GetUndoActions().Select(action => action.ToString()).ToList(),
            _editor.GetRedoActions().Select(action => action.ToString()).ToList());
    }
    #endregion

    #region Scheduler
    private SimProcess[] GetDefaultProcesses() =>
    [
        new SimProcess("P1", 10),
        new SimProcess("P2", 4),
        new SimProcess("P3", 7),
    ];

    private void ResetScheduler()
    {
        _robinTimer.Stop();
        RobinRunBtn.Content = "Запустить симуляцию";
        _schedulerReport = null;
        _schedulerSliceIdx = -1;

        // Canvas is empty until user runs simulation
        TimelineCanvas.SetData(new List<RoundRobinScheduler.ExecutionSlice>(), new List<SimProcess>(), -1);

        StatRobinTotalTime.Value = "—";
        StatRobinAvgWait.Value = "—";
        StatRobinAvgTurnaround.Value = "—";
        StatRobinSlicesCount.Value = "—";

        _robinLog.Clear();
        _robinLog.Add("Нажмите «Запустить симуляцию» для моделирования Round Robin");
    }

    private void ToggleRobinPlay()
    {
        if (_robinTimer.IsEnabled)
        {
            _robinTimer.Stop();
            RobinRunBtn.Content = "Продолжить";
            return;
        }

        if (_schedulerReport == null || _schedulerSliceIdx >= _schedulerReport.Slices.Count - 1)
        {
            int q = (int)RobinQuantumSlider.Value;
            _schedulerReport = RoundRobinScheduler.Simulate(GetDefaultProcesses(), q);
            _schedulerSliceIdx = -1;
        }

        _robinTimer.Start();
        RobinRunBtn.Content = "Пауза";
        OnRobinTick();
    }

    private void OnRobinTick()
    {
        StepScheduler();
        if (_schedulerReport != null && _schedulerSliceIdx >= _schedulerReport.Slices.Count - 1)
        {
            _robinTimer.Stop();
            RobinRunBtn.Content = "Запустить симуляцию";
        }
    }

    private void StepScheduler()
    {
        if (_schedulerReport == null)
        {
            int q = (int)RobinQuantumSlider.Value;
            _schedulerReport = RoundRobinScheduler.Simulate(GetDefaultProcesses(), q);
            _schedulerSliceIdx = -1;
        }

        _schedulerSliceIdx++;
        if (_schedulerSliceIdx < _schedulerReport.Slices.Count)
        {
            TimelineCanvas.SetData(_schedulerReport.Slices, _schedulerReport.Processes, _schedulerSliceIdx);
            var cur = _schedulerReport.Slices[_schedulerSliceIdx];
            _robinLog.Insert(0, $"t={cur.StartTime,2}..{cur.EndTime,2}: Выполняется {cur.ProcessName} (осталось {cur.RemainingAfter})");
        }
        else
        {
            TimelineCanvas.SetData(_schedulerReport.Slices, _schedulerReport.Processes, -1);
            StatRobinTotalTime.Value = $"{_schedulerReport.TotalTime} тактов";
            StatRobinAvgWait.Value = $"{_schedulerReport.AverageWaitTime:F2} тактов";
            StatRobinAvgTurnaround.Value = $"{_schedulerReport.AverageTurnaroundTime:F2} тактов";
            StatRobinSlicesCount.Value = _schedulerReport.Slices.Count.ToString();

            _robinLog.Clear();
            _robinLog.Add("Процесс | Время работы | Завершён | Ожидание");
            foreach (var p in _schedulerReport.Processes)
            {
                _robinLog.Add($"{p.Name,-7} | {p.Burst,12} | {p.FinishTime,8} | {p.WaitingTime,8}");
            }
        }
    }
    #endregion
}
