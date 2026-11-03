using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using AlgorithmsCore.Applications;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class ExpressionTreeView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _log = new();

    public ExpressionTreeView()
    {
        InitializeComponent();

        TreeLogList.ItemsSource = _log;

        BuildTreeBtn.Click += (_, _) => BuildAndEvaluate();
        PresetDocBtn.Click += (_, _) => SetPreset("7 8 + 3 2 + :");
        PresetComplexBtn.Click += (_, _) => SetPreset("3 4 2 * 1 5 - 2 3 ^ ^ / +");
        PresetFuncBtn.Click += (_, _) => SetPreset("16 sqrt 2 ^ 3 +");

        BuildAndEvaluate();
    }

    public bool HandleShortcut(Key key)
    {
        if (key == Key.Space)
        {
            BuildAndEvaluate();
            return true;
        }
        return false;
    }

    private void SetPreset(string postfix)
    {
        PostfixInput.Text = postfix;
        BuildAndEvaluate();
    }

    private void BuildAndEvaluate()
    {
        string postfix = PostfixInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(postfix)) return;

        try
        {
            var sw = Stopwatch.StartNew();
            var tree = ExpressionTree.FromPostfix(postfix);
            double result = tree.Evaluate();
            string infix = tree.Infix();
            string prefix = tree.Prefix();
            string post = tree.Postfix();
            sw.Stop();

            VisualCanvas.Tree = tree;

            StatResult.Value = $"{result:G8}";
            InfixResult.Text = infix;
            PrefixResult.Text = prefix;
            PostfixResult.Text = post;

            StatNodes.Value = tree.GetNodeCount().ToString();
            StatDepth.Value = tree.GetDepth().ToString();
            StatBuildTime.Value = TimeFormat.Format(sw.Elapsed);

            _log.Clear();
            _log.Add($"✓ Дерево успешно построено из: \"{postfix}\"");
            _log.Add($"  Вычислено значение: {result:G8}");
            _log.Add($"  Узлов: {tree.GetNodeCount()}, Глубина: {tree.GetDepth()}");
        }
        catch (Exception ex)
        {
            VisualCanvas.Tree = null;
            StatResult.Value = "Ошибка";
            InfixResult.Text = "—";
            PrefixResult.Text = "—";
            PostfixResult.Text = "—";
            _log.Insert(0, $"⚠ Ошибка разбора: {ex.Message}");
        }
    }
}
