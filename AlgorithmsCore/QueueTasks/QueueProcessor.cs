using AlgorithmsCore.DataStructures;

namespace AlgorithmsCore.QueueTasks;

using System;
using System.IO;


public class QueueProcessor
{
    public static void ProcessFile(string filePath, IQueue<string> queue)
    {
        string[] commands = File.ReadAllText(filePath).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var cmd in commands)
        {
            var parts = cmd.Split(',');
            int operation = int.Parse(parts[0]);

            switch (operation)
            {
                case 1:
                    queue.Enqueue(parts[1]);
                    Console.WriteLine($"Enqueue({parts[1]})");
                    break;
                case 2:
                    Console.WriteLine($"Dequeue() -> {queue.Dequeue()}");
                    break;
                case 3:
                    Console.WriteLine($"Top() -> {queue.Top()}");
                    break;
                case 4:
                    Console.WriteLine($"IsEmpty() -> {queue.IsEmpty()}");
                    break;
                case 5:
                    Console.Write("Print: ");
                    queue.Print();
                    Console.WriteLine();
                    break;
            }
        }
    }
}