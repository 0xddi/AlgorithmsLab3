using System.Diagnostics;

namespace AlgorithmsCore.Tasks;

public class StackPerformanceTester
{
    public static void RunTest(int operationCount)
    {
        string filePath = $"test_{operationCount}.txt";
        
        // Генерируем тестовый файл (чередуем Push и Pop)
        using (var sw = new StreamWriter(filePath))
        {
            for (int i = 0; i < operationCount; i++)
            {
                sw.Write(i % 2 == 0 ? $"1,item{i} " : "2 ");
            }
        }

        // Замеряем время
        var stopwatch = Stopwatch.StartNew();
        
        // Перенаправляем вывод в никуда, чтобы не засорять консоль при замере
        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null); 
        
        StackOperationProcessor.ProcessFile(filePath);
        
        Console.SetOut(originalOut);
        stopwatch.Stop();

        Console.WriteLine($"Операций: {operationCount}, Время: {stopwatch.ElapsedMilliseconds} мс");
        File.Delete(filePath); // Убираем за собой
    }
}