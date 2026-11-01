namespace AlgorithmsCore.Tasks;

public class StackOperationProcessor
{
    public static void ProcessFile(string filePath)
    {
        var stack = new DataStructures.Stack<string>();
        
        // Читаем весь файл и делим по пробелам
        string[] commands = File.ReadAllText(filePath).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var cmd in commands)
        {
            // Для пуш-операции команда выглядит как "1,значение"
            var parts = cmd.Split(',');
            int operation = int.Parse(parts[0]);

            switch (operation)
            {
                case 1:
                    stack.Push(parts[1]);
                    Console.WriteLine($"Push({parts[1]})");
                    break;
                case 2:
                    Console.WriteLine($"Pop() -> {stack.Pop()}");
                    break;
                case 3:
                    Console.WriteLine($"Top() -> {stack.Top()}");
                    break;
                case 4:
                    Console.WriteLine($"IsEmpty() -> {stack.IsEmpty()}");
                    break;
                case 5:
                    Console.Write("Print: ");
                    stack.Print();
                    Console.WriteLine();
                    break;
            }
        }
    }
}