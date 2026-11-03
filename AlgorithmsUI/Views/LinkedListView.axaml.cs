using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using AlgorithmsCore.LinkedListExtensions;
using AlgorithmsUI.Theme;

namespace AlgorithmsUI.Views;

public partial class LinkedListView : UserControl, IShortcutHandler
{
    private readonly ObservableCollection<string> _log = new();
    private AlgorithmsCore.DataStructures.LinkedList<int> _list = new();

    public LinkedListView()
    {
        InitializeComponent();

        ListLogList.ItemsSource = _log;

        ApplyListBtn.Click += (_, _) => LoadListFromInput();
        RandomListBtn.Click += (_, _) => GenerateRandomList();
        PresetSortedBtn.Click += (_, _) => LoadPreset("10 20 30 40 50 60 70 80");
        PresetDupesBtn.Click += (_, _) => LoadPreset("5 12 5 8 12 99 5 42 8");

        // 12 Operations
        Op1ReverseBtn.Click += (_, _) => ExecuteOp("1. Reverse", () => _list.Reverse());
        Op2LastToFirstBtn.Click += (_, _) => ExecuteOp("2а. MoveLastToFirst", () => _list.MoveLastToFirst());
        Op2FirstToLastBtn.Click += (_, _) => ExecuteOp("2б. MoveFirstToLast", () => _list.MoveFirstToLast());
        Op3CountDistinctBtn.Click += (_, _) =>
        {
            var sw = Stopwatch.StartNew();
            int count = _list.CountDistinct();
            sw.Stop();
            StatDistinct.Value = count.ToString();
            StatOpTime.Value = TimeFormat.Format(sw.Elapsed);
            _log.Insert(0, $"3. CountDistinct: найдено {count} уникальных элементов");
            VisualCanvas.StatusAction = $"Уникальных элементов: {count}";
        };
        Op4RemoveNonUniqueBtn.Click += (_, _) => ExecuteOp("4. RemoveNonUnique", () => _list.RemoveNonUnique());
        Op5InsertSelfBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op5XInput.Text, out int x))
                ExecuteOp($"5. InsertSelfAfter({x})", () => _list.InsertSelfAfter(x));
        };
        Op6InsertSortedBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op6EInput.Text, out int e))
                ExecuteOp($"6. InsertSorted({e})", () => _list.InsertSorted(e));
        };
        Op7RemoveAllBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op7EInput.Text, out int e))
                ExecuteOp($"7. RemoveAll({e})", () => _list.RemoveAll(e));
        };
        Op8InsertBeforeBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op8EInput.Text, out int e) && int.TryParse(Op8FInput.Text, out int f))
                ExecuteOp($"8. InsertBefore(E={e}, F={f})", () => _list.InsertBefore(e, f));
        };
        Op9AppendBtn.Click += (_, _) =>
        {
            var other = ParseList(Op9ListInput.Text ?? "");
            ExecuteOp($"9. Append([{string.Join(" ", other)}])", () => _list.Append(other));
        };
        Op10SplitBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op10TargetInput.Text, out int target))
            {
                var sw = Stopwatch.StartNew();
                var (first, second) = _list.SplitByFirstOccurrence(target);
                sw.Stop();
                VisualCanvas.PrimaryList = first.Select(x => x.ToString()).ToList();
                VisualCanvas.SecondaryList = second.Select(x => x.ToString()).ToList();
                VisualCanvas.StatusAction = $"10. SplitByFirstOccurrence({target}): Список 1 = {first.GetLength()} эл., Список 2 = {second.GetLength()} эл.";
                StatOpTime.Value = TimeFormat.Format(sw.Elapsed);
                _log.Insert(0, $"10. Split({target}) -> L1: [{string.Join(" ", first)}], L2: [{string.Join(" ", second)}]");
            }
        };
        Op11DoubleBtn.Click += (_, _) => ExecuteOp("11. Double", () => _list.Double());
        Op12SwapBtn.Click += (_, _) =>
        {
            if (int.TryParse(Op12Idx1Input.Text, out int i1) && int.TryParse(Op12Idx2Input.Text, out int i2))
            {
                VisualCanvas.SetHighlights(i1, i2);
                ExecuteOp($"12. Swap(индекс {i1} ↔ {i2})", () => _list.Swap(i1, i2));
            }
        };

        LoadListFromInput();
    }

    public bool HandleShortcut(Key key)
    {
        switch (key)
        {
            case Key.R: LoadListFromInput(); return true;
            case Key.N: GenerateRandomList(); return true;
        }
        return false;
    }

    private static AlgorithmsCore.DataStructures.LinkedList<int> ParseList(string text)
    {
        var result = new AlgorithmsCore.DataStructures.LinkedList<int>();
        var tokens = text.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            if (int.TryParse(t, out int val))
                result.AddLast(val);
        }
        return result;
    }

    private void LoadListFromInput()
    {
        _list = ParseList(ListInput.Text ?? "");
        UpdateVisuals("Список инициализирован");
    }

    private void LoadPreset(string preset)
    {
        ListInput.Text = preset;
        LoadListFromInput();
    }

    private void GenerateRandomList()
    {
        var rnd = new Random();
        int count = rnd.Next(6, 12);
        var nums = new List<int>();
        for (int i = 0; i < count; i++)
            nums.Add(rnd.Next(1, 100));

        ListInput.Text = string.Join(" ", nums);
        LoadListFromInput();
    }

    private void ExecuteOp(string opName, Action action)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();

            StatOpTime.Value = TimeFormat.Format(sw.Elapsed);
            _log.Insert(0, $"✓ {opName} выполнен за {TimeFormat.Format(sw.Elapsed)}");
            UpdateVisuals($"Успешно выполнено: {opName}");
        }
        catch (Exception ex)
        {
            _log.Insert(0, $"⚠ Ошибка в {opName}: {ex.Message}");
            VisualCanvas.StatusAction = $"Ошибка: {ex.Message}";
        }
    }

    private void UpdateVisuals(string actionText)
    {
        var items = _list.Select(x => x.ToString()).ToList();
        VisualCanvas.PrimaryList = items;
        VisualCanvas.SecondaryList = null; // reset split view
        VisualCanvas.StatusAction = actionText;

        StatListLength.Value = items.Count.ToString();
        StatHead.Value = _list.IsEmpty() ? "null" : _list.GetFirst().ToString();
        StatTail.Value = _list.IsEmpty() ? "null" : _list.GetLast().ToString();
        StatDistinct.Value = _list.CountDistinct().ToString();
    }
}
