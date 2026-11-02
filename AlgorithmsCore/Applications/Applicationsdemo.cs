namespace AlgorithmsCore.Applications;

// Демонстрация для защиты 3 части. Вызывать из Main: ApplicationsDemo.Run();
public static class ApplicationsDemo
{
    public static void Run()
    {
        ListDemo();
        StackDemo();
        QueueDemo();
        TreeDemo();
    }

    private static void ListDemo()
    {
        Console.WriteLine("=== СПИСОК: сложение длинных чисел ===");
        string a = "99999999999999999999999999";
        string b = "1";
        Console.WriteLine($"{a}\n+ {b}\n= {LongNumberAdder.Add(a, b)}");

        a = "123456789012345678901234567890";
        b = "987654321098765432109876543210";
        Console.WriteLine($"\n{a}\n+ {b}\n= {LongNumberAdder.Add(a, b)}\n");
    }

    private static void StackDemo()
    {
        Console.WriteLine("=== СТЕК: Undo/Redo ===");
        var editor = new UndoRedoEditor();

        editor.Type("Hello");
        Console.WriteLine($"Type(\"Hello\")    -> \"{editor.Text}\"");
        editor.Type(", world");
        Console.WriteLine($"Type(\", world\")  -> \"{editor.Text}\"");
        editor.Delete(3);
        Console.WriteLine($"Delete(3)        -> \"{editor.Text}\"");
        editor.Undo();
        Console.WriteLine($"Undo             -> \"{editor.Text}\"");
        editor.Undo();
        Console.WriteLine($"Undo             -> \"{editor.Text}\"");
        editor.Redo();
        Console.WriteLine($"Redo             -> \"{editor.Text}\"");
        editor.Type("!");
        Console.WriteLine($"Type(\"!\")        -> \"{editor.Text}\" (redo очищен)");
        Console.WriteLine($"Redo возможен?   -> {editor.Redo()}\n");
    }

    private static void QueueDemo()
    {
        Console.WriteLine("=== ОЧЕРЕДЬ: Round Robin ===");
        var processes = new[]
        {
            new SimProcess("P1", 10),
            new SimProcess("P2", 4),
            new SimProcess("P3", 7),
        };
        RoundRobinScheduler.Run(processes, quantum: 3);
        Console.WriteLine();
    }

    private static void TreeDemo()
    {
        Console.WriteLine("=== ДЕРЕВО: дерево выражений ===");
        string postfix = "7 8 + 3 2 + :";
        var tree = ExpressionTree.FromPostfix(postfix);

        Console.WriteLine($"Постфиксная запись: {postfix}");
        Console.WriteLine($"Инфиксный обход:    {tree.Infix()}");
        Console.WriteLine($"Префиксный обход:   {tree.Prefix()}");
        Console.WriteLine($"Постфиксный обход:  {tree.Postfix()}");
        Console.WriteLine($"Значение:           {tree.Evaluate()}");
    }
}