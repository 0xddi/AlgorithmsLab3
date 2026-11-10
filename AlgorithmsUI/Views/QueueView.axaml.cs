using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AlgorithmsCore.DataStructures;
using AlgorithmsCore.QueueTasks;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class QueueView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _opsLog = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _benchTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _benchClock = new();
    private readonly List<string> _benchWindow = new();
    private readonly Queue<int> _benchValues = new();

    private List<QueueProcessor.QueueStep> _opSteps = new();
    private int _currentOpStep = 0;
    private CancellationTokenSource? _tabTransition;

    // Benchmark replay state: the two measurements are instant, this only drives the visual replay.
    private int _benchTotal;
    private int _benchMixIndex;
    private int _benchNextIndex;
    private int _benchProcessed;
    private int _benchEnqueues;
    private int _benchDequeues;
    private int _benchSize;
    private double _benchDuration;
    private double _benchSampleAccumulator;
    private double _benchWindowAccumulator;
    private double _benchLastTime;
    private int _benchSliceEnqueueValue;
    private int _benchSliceDequeueValue;
    private bool _benchSliceHadEnqueue;
    private bool _benchSliceHadDequeue;
    private bool _benchRunning;
    private TimeSpan _benchMyElapsed;
    private TimeSpan _benchStdElapsed;

    public QueueView()
    {
        InitializeComponent();

        OpsLogList.ItemsSource = _opsLog;
        _timer.Tick += (_, _) => OnTimerTick();
        _benchTimer.Tick += (_, _) => OnBenchTick();

        TabOpsBtn.Click += (_, _) => SwitchTab(0);
        TabBenchBtn.Click += (_, _) => SwitchTab(1);

        RunAllBtn.Click += (_, _) => TogglePlay();
        StepBtn.Click += (_, _) =>
        {
            _timer.Stop();
            RunAllBtn.Content = "Старт (Авто)";
            StepOperation();
        };
        ResetBtn.Click += (_, _) => ResetOperations();
        PresetDocBtn.Click += (_, _) => SetPreset("3 4 1,56 1,7 1,cat 2 5 4");
        PresetRandomBtn.Click += (_, _) => GenerateRandomOperations();
        LoadFileBtn.Click += (_, _) => LoadFromFile();
        SaveFileBtn.Click += (_, _) => SaveToFile();

        RadioMyQueue.IsCheckedChanged += (_, _) => ResetOperations();
        RadioStdQueue.IsCheckedChanged += (_, _) => ResetOperations();

        RunCompareBtn.Click += (_, _) => RunComparison();

        ResetOperations();
    }

    private void SwitchTab(int tabIndex)
    {
        _timer.Stop();
        RunAllBtn.Content = "Старт (Авто)";

        TabOpsBtn.Classes.Set("accent", tabIndex == 0);
        TabBenchBtn.Classes.Set("accent", tabIndex == 1);

        bool benchmark = tabIndex == 1;
        if (benchmark != BenchCanvas.IsVisible)
        {
            BenchCanvas.IsVisible = benchmark;
            VisualCanvas.IsVisible = !benchmark;

            // Leaving the tab finishes the replay immediately: the measurements are already done, so the
            // result rows must not stay at "…".
            if (!benchmark)
                FinishBenchmarkReplay();
        }

        var panels = new[] { PanelOperations, PanelBenchmark };
        var target = panels[tabIndex];
        if (target.IsVisible)
            return;

        _tabTransition?.Cancel();
        _tabTransition?.Dispose();
        var transition = new CancellationTokenSource();
        _tabTransition = transition;
        _ = AnimateTabChangeAsync(target, panels, transition.Token);
    }

    private static async Task AnimateTabChangeAsync(
        Control target,
        IReadOnlyList<Control> panels,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var panel in panels)
            {
                panel.IsVisible = panel == target;
                panel.Opacity = 1;
                panel.RenderTransform = null;
            }

            await Motion.FadeSlideInAsync(target, 14, Motion.Normal, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer tab click owns the final state.
        }
    }

    public bool HandleShortcut(Key key)
    {
        if (PanelOperations.IsVisible)
        {
            switch (key)
            {
                case Key.Space: TogglePlay(); return true;
                case Key.Right: StepOperation(); return true;
                case Key.R: ResetOperations(); return true;
            }
        }
        return false;
    }

    private IQueue<string> CreateQueue()
    {
        return RadioMyQueue.IsChecked == true
            ? new MyQueue<string>()
            : new StandardQueue<string>();
    }

    private void PrepareSteps()
    {
        string input = CommandsInput.Text ?? "";
        var queue = CreateQueue();
        _opSteps = QueueProcessor.ProcessCommands(input, queue);
        _currentOpStep = 0;
    }

    private void ResetOperations()
    {
        _timer.Stop();
        RunAllBtn.Content = "Старт (Авто)";
        PrepareSteps();
        _opsLog.Clear();
        VisualCanvas.Items = Array.Empty<string>();
        VisualCanvas.StatusAction = $"Очередь готова ({ (RadioMyQueue.IsChecked == true ? "MyQueue" : "StandardQueue") })";
        StatCurrentSize.Value = "0";
        StatHeadElem.Value = "—";
        StatTailElem.Value = "—";
        StatStepProgress.Value = $"0 / {_opSteps.Count}";
        StatExecTime.Value = "—";
    }

    private void TogglePlay()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            RunAllBtn.Content = "Продолжить";
            return;
        }

        if (_opSteps.Count == 0 || _currentOpStep >= _opSteps.Count)
        {
            ResetOperations();
        }

        _timer.Start();
        RunAllBtn.Content = "Пауза";
        OnTimerTick();
    }

    private void OnTimerTick()
    {
        StepOperation();
        if (_currentOpStep >= _opSteps.Count)
        {
            _timer.Stop();
            RunAllBtn.Content = "Старт (Авто)";
        }
    }

    private void StepOperation()
    {
        if (_opSteps.Count == 0 || _currentOpStep >= _opSteps.Count)
            PrepareSteps();

        if (_currentOpStep >= _opSteps.Count) return;

        var step = _opSteps[_currentOpStep];
        _currentOpStep++;

        VisualCanvas.Items = step.QueueItems;
        VisualCanvas.StatusAction = $"Шаг {_currentOpStep}: {step.ResultText}";
        _opsLog.Insert(0, $"[{step.StepIndex}] {step.ResultText}");

        StatCurrentSize.Value = step.QueueItems.Count.ToString();
        StatHeadElem.Value = step.QueueItems.Count > 0 ? step.QueueItems[0] : "—";
        StatTailElem.Value = step.QueueItems.Count > 0 ? step.QueueItems[^1] : "—";
        StatStepProgress.Value = $"{_currentOpStep} / {_opSteps.Count}";
    }

    private void SetPreset(string preset)
    {
        CommandsInput.Text = preset;
        ResetOperations();
    }

    private void GenerateRandomOperations()
    {
        var rnd = new Random();
        var cmds = new List<string>();
        int count = rnd.Next(8, 16);
        for (int i = 0; i < count; i++)
        {
            int op = rnd.Next(1, 6);
            if (op == 1)
                cmds.Add($"1,{rnd.Next(1, 100)}");
            else
                cmds.Add(op.ToString());
        }
        CommandsInput.Text = string.Join(" ", cmds);
        ResetOperations();
    }

    private void LoadFromFile()
    {
        string path = "input.txt";
        if (File.Exists(path))
        {
            CommandsInput.Text = File.ReadAllText(path);
            ResetOperations();
            _opsLog.Insert(0, "✓ Данные успешно загружены из input.txt");
        }
        else
        {
            _opsLog.Insert(0, "⚠ Файл input.txt не найден.");
        }
    }

    private void SaveToFile()
    {
        string path = "input.txt";
        File.WriteAllText(path, CommandsInput.Text ?? "");
        _opsLog.Insert(0, "✓ Файл input.txt успешно сохранён");
    }

    private void RunComparison()
    {
        int count = BenchCountCombo.SelectedIndex switch
        {
            0 => 10000,
            1 => 50000,
            2 => 100000,
            3 => 250000,
            4 => 500000,
            _ => 50000
        };

        int mix = BenchMixCombo.SelectedIndex;

        // The measurements run in tight loops: no animation overhead may distort the timings.
        var myQueue = new MyQueue<string>();
        var swMy = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            bool isEnq = mix switch
            {
                1 => true,
                2 => (i % 7 != 0),
                _ => (i % 2 == 0)
            };

            if (isEnq || myQueue.IsEmpty())
                myQueue.Enqueue($"item_{i}");
            else
                myQueue.Dequeue();
        }
        swMy.Stop();

        var stdQueue = new StandardQueue<string>();
        var swStd = Stopwatch.StartNew();
        for (int i = 0; i < count; i++)
        {
            bool isEnq = mix switch
            {
                1 => true,
                2 => (i % 7 != 0),
                _ => (i % 2 == 0)
            };

            if (isEnq || stdQueue.IsEmpty())
                stdQueue.Enqueue($"item_{i}");
            else
                stdQueue.Dequeue();
        }
        swStd.Stop();

        _benchTimer.Stop();
        _benchRunning = false;

        _benchTotal = count;
        _benchMixIndex = mix;
        _benchNextIndex = 0;
        _benchProcessed = 0;
        _benchEnqueues = 0;
        _benchDequeues = 0;
        _benchSize = 0;
        _benchWindow.Clear();
        _benchValues.Clear();
        _benchSampleAccumulator = 0;
        _benchWindowAccumulator = 0;
        _benchLastTime = 0;
        _benchSliceEnqueueValue = 0;
        _benchSliceDequeueValue = 0;
        _benchSliceHadEnqueue = false;
        _benchSliceHadDequeue = false;
        _benchMyElapsed = swMy.Elapsed;
        _benchStdElapsed = swStd.Elapsed;

        _benchDuration = count switch
        {
            <= 10_000 => 2.4,
            <= 50_000 => 3.2,
            <= 100_000 => 3.6,
            <= 250_000 => 3.9,
            _ => 4.2
        };

        StatBenchOps.Value = $"0 / {count:N0}";
        StatMyQueueTime.Value = "…";
        StatStdQueueTime.Value = "…";
        StatDifference.Value = "…";
        StatRatio.Value = "…";

        BenchCanvas.Begin(count, MixLabel(mix));

        _benchClock.Restart();
        _benchRunning = true;
        _benchTimer.Start();
    }

    private void OnBenchTick()
    {
        if (!_benchRunning)
            return;

        double now = _benchClock.Elapsed.TotalSeconds;
        double delta = Math.Max(0, now - _benchLastTime);
        _benchLastTime = now;

        double progress = Math.Clamp(now / _benchDuration, 0, 1);
        int target = (int)Math.Round(_benchTotal * progress);

        while (_benchProcessed < target)
            ApplyBenchOperation();

        BenchCanvas.UpdateCounters(_benchProcessed, _benchEnqueues, _benchDequeues, _benchSize);
        StatBenchOps.Value = $"{_benchProcessed:N0} / {_benchTotal:N0}";

        _benchWindowAccumulator += delta;
        if (_benchWindowAccumulator >= 0.22 || progress >= 1)
        {
            _benchWindowAccumulator = 0;
            BenchCanvas.ShowWindow(_benchWindow, _benchSize > _benchWindow.Count);
        }

        _benchSampleAccumulator += delta;
        if (_benchSampleAccumulator >= 0.09 || progress >= 1)
        {
            _benchSampleAccumulator = 0;

            if (_benchSliceHadEnqueue)
                BenchCanvas.AddSample(true, $"вставка item_{_benchSliceEnqueueValue}");

            if (_benchSliceHadDequeue)
                BenchCanvas.AddSample(false, $"извлечение item_{_benchSliceDequeueValue}");

            _benchSliceHadEnqueue = false;
            _benchSliceHadDequeue = false;
        }

        if (progress >= 1)
            FinishBenchmarkReplay();
    }

    private void ApplyBenchOperation()
    {
        int index = _benchNextIndex++;
        bool isEnqueue = _benchMixIndex switch
        {
            1 => true,
            2 => (index % 7 != 0),
            _ => (index % 2 == 0)
        };

        if (isEnqueue || _benchSize == 0)
        {
            _benchSize++;
            _benchEnqueues++;
            _benchSliceHadEnqueue = true;
            _benchSliceEnqueueValue = index;

            // The drawing shows the tail of the queue: newest items stay visible while the head is far away.
            _benchWindow.Add($"item_{index}");
            while (_benchWindow.Count > 6)
                _benchWindow.RemoveAt(0);
        }
        else
        {
            _benchSize = Math.Max(0, _benchSize - 1);
            _benchDequeues++;
            _benchSliceHadDequeue = true;
            _benchSliceDequeueValue = _benchNextIndex - 1 - _benchDequeues;

            // Remove the visible head only while the shown window still contains it.
            if (_benchSize < _benchWindow.Count && _benchWindow.Count > 0)
                _benchWindow.RemoveAt(0);
        }

        _benchProcessed++;
    }

    private void FinishBenchmarkReplay()
    {
        if (!_benchRunning)
            return;

        _benchRunning = false;
        _benchTimer.Stop();

        while (_benchProcessed < _benchTotal)
            ApplyBenchOperation();

        BenchCanvas.UpdateCounters(_benchTotal, _benchEnqueues, _benchDequeues, _benchSize);
        BenchCanvas.ShowWindow(_benchWindow, _benchSize > _benchWindow.Count);

        // Compare real elapsed values instead of rounded milliseconds: at tens of thousands of operations
        // the difference often lives below one millisecond, and rounding made the verdict contradict the
        // highlighted time badges on the canvas.
        double myMs = _benchMyElapsed.TotalMilliseconds;
        double stdMs = _benchStdElapsed.TotalMilliseconds;
        double ratio = stdMs > 0 ? myMs / stdMs : 1.0;
        string verdict = ratio >= 1.0
            ? $"StandardQueue быстрее в {ratio:F1}x"
            : $"MyQueue быстрее в {(1.0 / ratio):F1}x";

        BenchCanvas.Complete(_benchMyElapsed, _benchStdElapsed, verdict);

        StatBenchOps.Value = $"{_benchTotal:N0}";
        StatMyQueueTime.Value = TimeFormat.Format(_benchMyElapsed);
        StatStdQueueTime.Value = TimeFormat.Format(_benchStdElapsed);
        StatDifference.Value = $"Δ = {Math.Abs(myMs - stdMs):F2} мс";
        StatRatio.Value = verdict;
    }

    private static string MixLabel(int mix) => mix switch
    {
        1 => "100% вставки",
        2 => "85% вставки / 15% извлечения",
        _ => "50% вставки / 50% извлечения"
    };
}
