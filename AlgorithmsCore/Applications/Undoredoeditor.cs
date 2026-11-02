namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ СТЕКА: Undo/Redo в текстовом редакторе.
// Два стека: в undo лежат выполненные действия (последнее сверху, LIFO),
// в redo лежат отменённые. Новое действие очищает redo, как в настоящих редакторах.
// Все операции стека O(1).
public class UndoRedoEditor
{
    // Действие хранит и добавленный, и удалённый текст, чтобы его можно было отменить
    private record EditAction(string Added, string Removed);

    private string text = "";
    private DataStructures.Stack<EditAction> undoStack = new();
    private DataStructures.Stack<EditAction> redoStack = new();

    public string Text => text;

    // Напечатать текст в конец
    public void Type(string added)
    {
        text += added;
        undoStack.Push(new EditAction(added, ""));
        redoStack = new DataStructures.Stack<EditAction>(); // ветка redo больше не актуальна
    }

    // Удалить count последних символов
    public void Delete(int count)
    {
        count = Math.Min(count, text.Length);
        if (count <= 0) return;

        string removed = text.Substring(text.Length - count);
        text = text.Substring(0, text.Length - count);
        undoStack.Push(new EditAction("", removed));
        redoStack = new DataStructures.Stack<EditAction>();
    }

    public bool Undo()
    {
        if (undoStack.IsEmpty()) return false;

        var action = undoStack.Pop();
        text = text.Substring(0, text.Length - action.Added.Length) + action.Removed;
        redoStack.Push(action);
        return true;
    }

    public bool Redo()
    {
        if (redoStack.IsEmpty()) return false;

        var action = redoStack.Pop();
        text = text.Substring(0, text.Length - action.Removed.Length) + action.Added;
        undoStack.Push(action);
        return true;
    }
}