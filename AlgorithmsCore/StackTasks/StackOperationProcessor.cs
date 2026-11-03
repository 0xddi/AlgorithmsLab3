namespace AlgorithmsCore.Tasks;

public class StackOperationProcessor
{
    public record OperationStep(
        int StepIndex,
        int OperationCode,
        string CommandRaw,
        string? Argument,
        string ResultText,
        List<string> StackItems);

    public static List<OperationStep> ProcessCommands(string input)
    {
        var stack = new DataStructures.Stack<string>();
        var steps = new List<OperationStep>();
        string[] commands = input.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int stepIdx = 0;

        foreach (var cmd in commands)
        {
            stepIdx++;
            var parts = cmd.Split(',');
            if (!int.TryParse(parts[0], out int operation))
                continue;

            string? arg = parts.Length > 1 ? parts[1] : null;
            string resultText = "";

            switch (operation)
            {
                case 1: // Push(elem)
                    if (arg != null)
                    {
                        stack.Push(arg);
                        resultText = $"Push(\"{arg}\") — успешно добавлен";
                    }
                    else
                    {
                        resultText = "Push() — ошибка: отсутствует аргумент";
                    }
                    break;

                case 2: // Pop()
                    if (stack.IsEmpty())
                    {
                        resultText = "Pop() — исключение: стек пуст";
                    }
                    else
                    {
                        var popped = stack.Pop();
                        resultText = $"Pop() -> \"{popped}\"";
                    }
                    break;

                case 3: // Top()
                    if (stack.IsEmpty())
                    {
                        resultText = "Top() — стек пуст";
                    }
                    else
                    {
                        resultText = $"Top() -> \"{stack.Top()}\"";
                    }
                    break;

                case 4: // IsEmpty()
                    resultText = $"IsEmpty() -> {stack.IsEmpty()}";
                    break;

                case 5: // Print()
                    resultText = stack.IsEmpty() 
                        ? "Print() -> [пусто]" 
                        : $"Print() -> [{string.Join(" ", stack.ToList())}]";
                    break;

                default:
                    resultText = $"Неизвестная операция {operation}";
                    break;
            }

            steps.Add(new OperationStep(stepIdx, operation, cmd, arg, resultText, stack.ToList()));
        }

        return steps;
    }

    public static void ProcessFile(string filePath)
    {
        var steps = ProcessCommands(File.ReadAllText(filePath));
        foreach (var step in steps)
        {
            Console.WriteLine(step.ResultText);
        }
    }
}
