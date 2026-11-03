using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using AlgorithmsCore.Tasks;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class StackView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _opsLog = new();
    private readonly ObservableCollection<string> _calcLog = new();

    private List<StackOperationProcessor.OperationStep> _opSteps = new();
    private int _currentOpStep = 0;

    private List<PostfixEvaluator.EvalStep> _evalSteps = new();
    private int _currentEvalStep = 0;

    public StackView()
    {
        InitializeComponent();

        OpsLogList.ItemsSource = _opsLog;
        CalcLogList.ItemsSource = _calcLog;

        // Tabs
        TabOpsBtn.Click += (_, _) => SwitchTab(0);
        TabCalcBtn.Click += (_, _) => SwitchTab(1);
        TabBenchBtn.Click += (_, _) => SwitchTab(2);

        // Operations controls
        RunAllBtn.Click += (_, _) => RunAllOperations();
        StepBtn.Click += (_, _) => StepOperation();
        ResetBtn.Click += (_, _) => ResetOperations();
        PresetDocBtn.Click += (_, _) => SetPreset("3 4 1,56 1,7 1,cat 2 5 4");
        PresetRandomBtn.Click += (_, _) => GenerateRandomOperations();
        LoadFileBtn.Click += (_, _) => LoadFromFile();
        SaveFileBtn.Click += (_, _) => SaveToFile();

        // Calculator controls
        ConvertInfixBtn.Click += (_, _) => ConvertInfix();
        EvalPostfixBtn.Click += (_, _) => EvalAllPostfix();
        EvalStepBtn.Click += (_, _) => StepEvalPostfix();

        // Benchmark
        RunBenchBtn.Click += (_, _) => RunBenchmark();

        ResetOperations();
    }

    private void SwitchTab(int tabIndex)
    {
        PanelOperations.IsVisible = (tabIndex == 0);
        PanelCalculator.IsVisible = (tabIndex == 1);
        PanelBenchmark.IsVisible = (tabIndex == 2);

        TabOpsBtn.Classes.Set("accent", tabIndex == 0);
        TabCalcBtn.Classes.Set("accent", tabIndex == 1);
        TabBenchBtn.Classes.Set("accent", tabIndex == 2);
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
        else if (PanelCalculator.IsVisible)
        {
            switch (key)
            {
                case Key.Space: EvalAllPostfix(); return true;
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
        PrepareSteps();
        _opsLog.Clear();
        VisualCanvas.Items = Array.Empty<string>();
        VisualCanvas.StatusAction = "Стек готов к выполнению";
        StatCurrentSize.Value = "0";
        StatTopElem.Value = "—";
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

        VisualCanvas.Items = step.StackItems;
        VisualCanvas.StatusAction = $"Шаг {_currentOpStep}: {step.ResultText}";
        _opsLog.Insert(0, $"[{step.StepIndex}] {step.ResultText}");

        StatCurrentSize.Value = step.StackItems.Count.ToString();
        StatTopElem.Value = step.StackItems.Count > 0 ? step.StackItems[0] : "—";
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
            VisualCanvas.Items = last.StackItems;
            VisualCanvas.StatusAction = $"Завершено ({_opSteps.Count} операций)";
            StatCurrentSize.Value = last.StackItems.Count.ToString();
            StatTopElem.Value = last.StackItems.Count > 0 ? last.StackItems[0] : "—";
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
            _opsLog.Insert(0, "⚠ Файл input.txt не найден. Сохраните данные сперва.");
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

    private void EvalAllPostfix()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            PrepareEval();
            _calcLog.Clear();

            foreach (var step in _evalSteps)
            {
                _calcLog.Add($"[{step.StepIndex}] Токен '{step.Token}': {step.Action}");
            }
            sw.Stop();

            if (_evalSteps.Count > 0)
            {
                var last = _evalSteps.Last();
                VisualCanvas.Items = last.StackState.Select(v => v.ToString("G6")).ToList();
                VisualCanvas.StatusAction = $"Результат: {last.ProducedValue:G6}";
                StatCalcResult.Value = $"{last.ProducedValue:G6}";
                StatCalcStep.Value = $"{_evalSteps.Count} / {_evalSteps.Count}";
            }
            StatCalcTime.Value = TimeFormat.Format(sw.Elapsed);
        }
        catch (Exception ex)
        {
            _calcLog.Insert(0, $"Ошибка: {ex.Message}");
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

        int mix = BenchMixCombo.SelectedIndex; // 0: 50/50, 1: 100% push, 2: 80/20

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

        StatBenchOps.Value = $"{count:N0}";
        StatBenchTime.Value = TimeFormat.Format(sw.Elapsed);
        double opsPerSec = sw.Elapsed.TotalSeconds > 0 ? count / sw.Elapsed.TotalSeconds : 0;
        StatBenchSpeed.Value = $"{opsPerSec:N0} оп/сек";
        double avgMicros = (sw.Elapsed.TotalMicroseconds) / count;
        StatBenchAvg.Value = $"{avgMicros:F3} мкс / оп (O(1))";

        VisualCanvas.Items = stack.ToList().Take(15).ToList();
        VisualCanvas.StatusAction = $"Тест {count:N0} операций завершён за {sw.ElapsedMilliseconds} мс";
    }
    #endregion
}
