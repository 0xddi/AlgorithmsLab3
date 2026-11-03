using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using AlgorithmsCore.DataStructures;
using AlgorithmsCore.QueueTasks;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class QueueView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _opsLog = new();
    private List<QueueProcessor.QueueStep> _opSteps = new();
    private int _currentOpStep = 0;

    public QueueView()
    {
        InitializeComponent();

        OpsLogList.ItemsSource = _opsLog;

        TabOpsBtn.Click += (_, _) => SwitchTab(0);
        TabBenchBtn.Click += (_, _) => SwitchTab(1);

        RunAllBtn.Click += (_, _) => RunAllOperations();
        StepBtn.Click += (_, _) => StepOperation();
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
        PanelOperations.IsVisible = (tabIndex == 0);
        PanelBenchmark.IsVisible = (tabIndex == 1);

        TabOpsBtn.Classes.Set("accent", tabIndex == 0);
        TabBenchBtn.Classes.Set("accent", tabIndex == 1);
    }

    public bool HandleShortcut(Key key)
    {
        if (PanelOperations.IsVisible)
        {
            switch (key)
            {
                case Key.Space: RunAllOperations(); return true;
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

    private void RunAllOperations()
    {
        var sw = Stopwatch.StartNew();
        PrepareSteps();
        _opsLog.Clear();

        foreach (var step in _opSteps)
        {
            _opsLog.Add($"[{step.StepIndex}] {step.ResultText}");
        }
        sw.Stop();

        if (_opSteps.Count > 0)
        {
            var last = _opSteps.Last();
            VisualCanvas.Items = last.QueueItems;
            VisualCanvas.StatusAction = $"Завершено ({_opSteps.Count} операций)";
            StatCurrentSize.Value = last.QueueItems.Count.ToString();
            StatHeadElem.Value = last.QueueItems.Count > 0 ? last.QueueItems[0] : "—";
            StatTailElem.Value = last.QueueItems.Count > 0 ? last.QueueItems[^1] : "—";
            StatStepProgress.Value = $"{_opSteps.Count} / {_opSteps.Count}";
        }
        StatExecTime.Value = TimeFormat.Format(sw.Elapsed);
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

        int mix = BenchMixCombo.SelectedIndex; // 0: mixed 50/50, 1: 100% enqueue, 2: 85/15

        // Benchmark MyQueue
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

        // Benchmark StandardQueue
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

        long myMs = swMy.ElapsedMilliseconds;
        long stdMs = swStd.ElapsedMilliseconds;
        double ratio = stdMs > 0 ? (double)myMs / stdMs : 1.0;

        StatBenchOps.Value = $"{count:N0}";
        StatMyQueueTime.Value = $"{myMs} мс ({TimeFormat.Format(swMy.Elapsed)})";
        StatStdQueueTime.Value = $"{stdMs} мс ({TimeFormat.Format(swStd.Elapsed)})";
        StatDifference.Value = $"Δ = {Math.Abs(myMs - stdMs)} мс";
        StatRatio.Value = ratio >= 1.0 ? $"StandardQueue быстрее в {ratio:F1}x" : $"MyQueue быстрее в {(1.0 / ratio):F1}x";

        VisualCanvas.Items = myQueue.ToList().Take(12).ToList();
        VisualCanvas.StatusAction = $"Сравнение завершено: MyQueue = {myMs} мс vs StandardQueue = {stdMs} мс";
    }
}
