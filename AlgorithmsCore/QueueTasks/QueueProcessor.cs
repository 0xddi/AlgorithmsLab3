using AlgorithmsCore.DataStructures;

namespace AlgorithmsCore.QueueTasks;

public class QueueProcessor
{
    public record QueueStep(
        int StepIndex,
        int OperationCode,
        string CommandRaw,
        string? Argument,
        string ResultText,
        List<string> QueueItems);

    public static List<QueueStep> ProcessCommands(string input, IQueue<string> queue)
    {
        var steps = new List<QueueStep>();
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
                case 1: // Enqueue
                    if (arg != null)
                    {
                        queue.Enqueue(arg);
                        resultText = $"Enqueue(\"{arg}\") — добавлен в хвост";
                    }
                    else
                    {
                        resultText = "Enqueue() — ошибка: отсутствует аргумент";
                    }
                    break;

                case 2: // Dequeue
                    if (queue.IsEmpty())
                    {
                        resultText = "Dequeue() — исключение: очередь пуста";
                    }
                    else
                    {
                        var dequeued = queue.Dequeue();
                        resultText = $"Dequeue() -> \"{dequeued}\"";
                    }
                    break;

                case 3: // Top / Peek
                    if (queue.IsEmpty())
                    {
                        resultText = "Top() — очередь пуста";
                    }
                    else
                    {
                        resultText = $"Top() -> \"{queue.Top()}\"";
                    }
                    break;

                case 4: // IsEmpty
                    resultText = $"IsEmpty() -> {queue.IsEmpty()}";
                    break;

                case 5: // Print
                    resultText = queue.IsEmpty()
                        ? "Print() -> [пусто]"
                        : $"Print() -> [{string.Join(" ", queue.ToList())}]";
                    break;

                default:
                    resultText = $"Неизвестная операция {operation}";
                    break;
            }

            steps.Add(new QueueStep(stepIdx, operation, cmd, arg, resultText, queue.ToList()));
        }

        return steps;
    }

    public static void ProcessFile(string filePath, IQueue<string> queue)
    {
        var steps = ProcessCommands(File.ReadAllText(filePath), queue);
        foreach (var step in steps)
        {
            Console.WriteLine(step.ResultText);
        }
    }
}
