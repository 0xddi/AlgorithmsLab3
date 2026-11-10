using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AlgorithmsCore.Tasks;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class StackView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _opsLog = new();
    private readonly ObservableCollection<string> _calcLog = new();

    private readonly DispatcherTimer _opsTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _evalTimer = new() { Interval = TimeSpan.FromMilliseconds(900) };
    private readonly DispatcherTimer _benchTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _benchClock = new();
    private readonly List<int> _benchStack = new();

    private List<StackOperationProcessor.OperationStep> _opSteps = new();
    private int _currentOpStep = 0;

    private List<PostfixEvaluator.EvalStep> _evalSteps = new();
    private int _currentEvalStep = 0;
    private CancellationTokenSource? _tabTransition;

    // Benchmark replay state: the measurement itself is instant, this only drives the visual replay.
    private int _benchTotal;
    private int _benchMixIndex;
    private int _benchNextIndex;
    private int _benchProcessed;
    private int _benchPushes;
    private int _benchPops;
    private int _benchSize;
    private double _benchDuration;
    private double _benchDisplayAccumulator;
    private double _benchLastTime;
    private int _benchLastValue;
    private bool _benchLastWasPush;
    private bool _benchRunning;
    private TimeSpan _benchMeasured;

    public StackView()
    {
        InitializeComponent();

        OpsLogList.ItemsSource = _opsLog;
        CalcLogList.ItemsSource = _calcLog;

        _opsTimer.Tick += (_, _) => OnOpsTick();
        _evalTimer.Tick += (_, _) => OnEvalTick();
        _benchTimer.Tick += (_, _) => OnBenchTick();

        // Tabs
        TabOpsBtn.Click += (_, _) => SwitchTab(0);
        TabCalcBtn.Click += (_, _) => SwitchTab(1);
        TabBenchBtn.Click += (_, _) => SwitchTab(2);

        // Operations controls
        RunAllBtn.Click += (_, _) => ToggleOpsPlay();
        StepBtn.Click += (_, _) =>
        {
            _opsTimer.Stop();
            RunAllBtn.Content = "Старт (Авто)";
            StepOperation();
        };
        ResetBtn.Click += (_, _) => ResetOperations();
        PresetDocBtn.Click += (_, _) => SetPreset("3 4 1,56 1,7 1,cat 2 5 4");
        PresetRandomBtn.Click += (_, _) => GenerateRandomOperations();
        LoadFileBtn.Click += (_, _) => LoadFromFile();
        SaveFileBtn.Click += (_, _) => SaveToFile();

        // Calculator controls
        ConvertInfixBtn.Click += (_, _) => ConvertInfix();
        EvalPostfixBtn.Click += (_, _) => ToggleEvalPlay();
        EvalStepBtn.Click += (_, _) =>
        {
            _evalTimer.Stop();
            EvalPostfixBtn.Content = "Вычислить (Авто)";
            StepEvalPostfix();
        };

        // Benchmark
        RunBenchBtn.Click += (_, _) => RunBenchmark();

        ResetOperations();
    }

    private void SwitchTab(int tabIndex)
    {
        _opsTimer.Stop();
        RunAllBtn.Content = "Старт (Авто)";
        _evalTimer.Stop();
        EvalPostfixBtn.Content = "Вычислить (Авто)";

        TabOpsBtn.Classes.Set("accent", tabIndex == 0);
        TabCalcBtn.Classes.Set("accent", tabIndex == 1);
        TabBenchBtn.Classes.Set("accent", tabIndex == 2);

        bool benchmark = tabIndex == 2;
        if (benchmark != BenchCanvas.IsVisible)
        {
            BenchCanvas.IsVisible = benchmark;
            VisualCanvas.IsVisible = !benchmark;

            // Leaving the tab finishes the replay immediately: the measurement is already done, so the
            // result rows must not stay at "…".
            if (!benchmark)
                FinishBenchmarkReplay();
        }

        var panels = new[] { PanelOperations, PanelCalculator, PanelBenchmark };
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
                case Key.Space: ToggleOpsPlay(); return true;
                case Key.Right: StepOperation(); return true;
                case Key.R: ResetOperations(); return true;
            }
        }
        else if (PanelCalculator.IsVisible)
        {
            switch (key)
            {
                case Key.Space: ToggleEvalPlay(); return true;
                case Key.Right: StepEvalPostfix(); return true;
            }
        }
        return false;
    }

    #region Operations
    private void PrepareSteps()
    {
        string input = CommandsInput.Text ?? "";
        _opSteps = StackOperationProcessor.ProcessCommands(input);
        _currentOpStep = 0;
    }

    private void ResetOperations()
    {
        _opsTimer.Stop();
        RunAllBtn.Content = "Старт (Авто)";
        PrepareSteps();
        _opsLog.Clear();
        VisualCanvas.Items = Array.Empty<string>();
        VisualCanvas.StatusAction = "Стек готов к выполнению";
        StatCurrentSize.Value = "0";
        StatTopElem.Value = "—";
        StatStepProgress.Value = $"0 / {_opSteps.Count}";
        StatExecTime.Value = "—";
    }

    private void ToggleOpsPlay()
    {
        if (_opsTimer.IsEnabled)
        {
            _opsTimer.Stop();
            RunAllBtn.Content = "Продолжить";
            return;
        }

        if (_opSteps.Count == 0 || _currentOpStep >= _opSteps.Count)
        {
            ResetOperations();
        }

        _opsTimer.Start();
        RunAllBtn.Content = "Пауза";
        OnOpsTick();
    }

    private void OnOpsTick()
    {
        StepOperation();
        if (_currentOpStep >= _opSteps.Count)
        {
            _opsTimer.Stop();
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

        VisualCanvas.Items = step.StackItems;
        VisualCanvas.StatusAction = $"Шаг {_currentOpStep}: {step.ResultText}";
        _opsLog.Insert(0, $"[{step.StepIndex}] {step.ResultText}");

        StatCurrentSize.Value = step.StackItems.Count.ToString();
        StatTopElem.Value = step.StackItems.Count > 0 ? step.StackItems[0] : "—";
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
    #endregion

    #region Calculator
    private void ConvertInfix()
    {
        try
        {
            string infix = InfixInput.Text ?? "";
            var (postfix, steps) = InfixToPostfixConverter.ConvertWithTrace(infix);
            ConvertedPostfixResult.Text = postfix;
            PostfixInput.Text = postfix;

            _calcLog.Clear();
            _calcLog.Add($"Инфикс: {infix}");
            _calcLog.Add($"Постфикс: {postfix}");
            _calcLog.Add("--- Шаги трансляции ---");
            foreach (var s in steps)
            {
                _calcLog.Add($"[{s.StepIndex}] Токен '{s.Token}': {s.Action} | Вывод: {string.Join(" ", s.OutputTokens)}");
            }
        }
        catch (Exception ex)
        {
            _calcLog.Insert(0, $"Ошибка перевода: {ex.Message}");
        }
    }

    private void PrepareEval()
    {
        string postfix = PostfixInput.Text ?? "";
        try
        {
            var (_, steps) = PostfixEvaluator.EvaluateWithTrace(postfix);
            _evalSteps = steps;
            _currentEvalStep = 0;
        }
        catch (Exception ex)
        {
            _evalSteps.Clear();
            _calcLog.Insert(0, $"Ошибка разбора: {ex.Message}");
        }
    }

    private void ToggleEvalPlay()
    {
        if (_evalTimer.IsEnabled)
        {
            _evalTimer.Stop();
            EvalPostfixBtn.Content = "Продолжить";
            return;
        }

        if (_evalSteps.Count == 0 || _currentEvalStep >= _evalSteps.Count)
        {
            PrepareEval();
            _calcLog.Clear();
        }

        _evalTimer.Start();
        EvalPostfixBtn.Content = "Пауза";
        OnEvalTick();
    }

    private void OnEvalTick()
    {
        StepEvalPostfix();
        if (_currentEvalStep >= _evalSteps.Count)
        {
            _evalTimer.Stop();
            EvalPostfixBtn.Content = "Вычислить (Авто)";
        }
    }

    private void StepEvalPostfix()
    {
        if (_evalSteps.Count == 0 || _currentEvalStep >= _evalSteps.Count)
            PrepareEval();

        if (_currentEvalStep >= _evalSteps.Count) return;

        var step = _evalSteps[_currentEvalStep];
        _currentEvalStep++;

        VisualCanvas.Items = step.StackState.Select(v => v.ToString("G6")).ToList();
        VisualCanvas.StatusAction = $"Шаг {_currentEvalStep}: {step.Action}";
        _calcLog.Insert(0, $"[{step.StepIndex}] Токен '{step.Token}': {step.Action}");

        StatCalcStep.Value = $"{_currentEvalStep} / {_evalSteps.Count}";
        if (_currentEvalStep == _evalSteps.Count && step.ProducedValue.HasValue)
        {
            StatCalcResult.Value = step.ProducedValue.Value.ToString("G6");
        }
    }
    #endregion

    #region Benchmark
    private void RunBenchmark()
    {
        int count = BenchCountCombo.SelectedIndex switch
        {
            0 => 1000,
            1 => 10000,
            2 => 50000,
            3 => 100000,
            4 => 500000,
            _ => 10000
        };

        int mix = BenchMixCombo.SelectedIndex;

        // The measurement runs in one tight loop: animation overhead must not distort the numbers.
        var stack = new AlgorithmsCore.DataStructures.Stack<string>();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < count; i++)
        {
            bool isPush = mix switch
            {
                1 => true,
                2 => (i % 5 != 0),
                _ => (i % 2 == 0)
            };

            if (isPush || stack.IsEmpty())
                stack.Push($"item_{i}");
            else
                stack.Pop();
        }

        sw.Stop();

        _benchTimer.Stop();
        _benchRunning = false;

        _benchTotal = count;
        _benchMixIndex = mix;
        _benchNextIndex = 0;
        _benchProcessed = 0;
        _benchPushes = 0;
        _benchPops = 0;
        _benchSize = 0;
        _benchStack.Clear();
        _benchDisplayAccumulator = 0;
        _benchLastTime = 0;
        _benchLastValue = 0;
        _benchLastWasPush = false;
        _benchMeasured = sw.Elapsed;

        // A fixed, brisk replay: enough to see pushes and pops, short enough for 500 000 operations.
        _benchDuration = count switch
        {
            <= 1_000 => 2.2,
            <= 10_000 => 3.2,
            <= 50_000 => 3.6,
            <= 100_000 => 3.8,
            _ => 4.2
        };

        StatBenchOps.Value = $"0 / {count:N0}";
        StatBenchTime.Value = "…";
        StatBenchSpeed.Value = "…";
        StatBenchAvg.Value = "…";

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

        BenchCanvas.UpdateCounters(_benchProcessed, _benchPushes, _benchPops);
        StatBenchOps.Value = $"{_benchProcessed:N0} / {_benchTotal:N0}";

        // Update the stack picture and the operation stream a few times per second: fast enough to feel
        // live, slow enough that each push/pop can be seen instead of blurring into one frame.
        _benchDisplayAccumulator += delta;
        if (_benchDisplayAccumulator >= 0.09 || progress >= 1)
        {
            _benchDisplayAccumulator = 0;
            BenchCanvas.ShowStack(_benchSize, TopItems());
            BenchCanvas.AddSample(
                _benchLastWasPush,
                _benchLastWasPush ? $"push item_{_benchLastValue}" : $"pop item_{_benchLastValue}");
        }

        if (progress >= 1)
            FinishBenchmarkReplay();
    }

    private void ApplyBenchOperation()
    {
        int index = _benchNextIndex++;
        bool isPush = _benchMixIndex switch
        {
            1 => true,
            2 => (index % 5 != 0),
            _ => (index % 2 == 0)
        };

        if (isPush || _benchSize == 0)
        {
            _benchStack.Insert(0, index);
            if (_benchStack.Count > 60)
                _benchStack.RemoveAt(60);

            _benchSize++;
            _benchPushes++;
            _benchLastWasPush = true;
            _benchLastValue = index;
        }
        else
        {
            _benchLastValue = _benchStack.Count > 0 ? _benchStack[0] : 0;
            if (_benchStack.Count > 0)
                _benchStack.RemoveAt(0);

            _benchSize = Math.Max(0, _benchSize - 1);
            _benchPops++;
            _benchLastWasPush = false;
        }

        _benchProcessed++;
    }

    private List<string> TopItems() =>
        _benchStack.Take(12).Select(value => $"item_{value}").ToList();

    private void FinishBenchmarkReplay()
    {
        if (!_benchRunning)
            return;

        _benchRunning = false;
        _benchTimer.Stop();

        while (_benchProcessed < _benchTotal)
            ApplyBenchOperation();

        BenchCanvas.UpdateCounters(_benchTotal, _benchPushes, _benchPops);
        BenchCanvas.ShowStack(_benchSize, TopItems());
        BenchCanvas.Complete($"{_benchTotal:N0} операций за {TimeFormat.Format(_benchMeasured)}");

        StatBenchOps.Value = $"{_benchTotal:N0}";
        StatBenchTime.Value = TimeFormat.Format(_benchMeasured);

        double seconds = _benchMeasured.TotalSeconds;
        StatBenchSpeed.Value = seconds > 0 ? $"{_benchTotal / seconds:N0} оп/сек" : "—";
        double avgMicros = _benchMeasured.TotalMicroseconds / Math.Max(1, _benchTotal);
        StatBenchAvg.Value = $"{avgMicros:F3} мкс / оп (O(1))";

    }

    private static string MixLabel(int mix) => mix switch
    {
        1 => "100% Push",
        2 => "80% Push / 20% Pop",
        _ => "50% Push / 50% Pop"
    };
    #endregion
}
