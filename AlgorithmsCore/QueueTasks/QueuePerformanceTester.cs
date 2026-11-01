using AlgorithmsCore.DataStructures;

namespace AlgorithmsCore.QueueTasks;

using System;
using System.Diagnostics;
using System.IO;

public class QueuePerformanceTester
{
    public static void Run()
    {
        // Набор 1: Разная длина (10 000 и 100 000 смешанных операций)
        TestCase("Смешанные (10k)", GenerateMixed(10000));
        TestCase("Смешанные (100k)", GenerateMixed(100000));
        
        // Набор 2: Одинаковая длина (10 000), но разный состав
        TestCase("Только вставки (10k)", GenerateAllEnqueue(10000));
    }

    private static void TestCase(string name, string filePath)
    {
        Console.WriteLine($"\n--- {name} ---");
        
        // Тест твоей очереди
        var customQueue = new DataStructures.MyQueue<string>();
        MeasureTime("Своя очередь", filePath, customQueue);

        // Тест стандартной очереди
        var stdQueue = new StandardQueue<string>();
        MeasureTime("Стандартная  ", filePath, stdQueue);

        File.Delete(filePath);
    }

    private static void MeasureTime(string label, string filePath, IQueue<string> queue)
    {
        var sw = Stopwatch.StartNew();
        
        // Отключаем вывод, чтобы он не тормозил замер
        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        
        QueueProcessor.ProcessFile(filePath, queue);
        
        Console.SetOut(originalOut);
        sw.Stop();
        
        Console.WriteLine($"{label}: {sw.ElapsedMilliseconds} мс");
    }

    private static string GenerateMixed(int count)
    {
        string path = $"queue_mixed_{count}.txt";
        using var sw = new StreamWriter(path);
        for (int i = 0; i < count; i++)
        {
            // Чередуем добавление и удаление, чтобы очередь не росла до гигантских размеров
            sw.Write(i % 2 == 0 ? $"1,item{i} " : "2 ");
        }
        return path;
    }

    private static string GenerateAllEnqueue(int count)
    {
        string path = $"queue_pure_{count}.txt";
        using var sw = new StreamWriter(path);
        for (int i = 0; i < count; i++)
        {
            sw.Write($"1,item{i} "); // Только добавления — убиваем O(n) на AddLast
        }
        return path;
    }
}