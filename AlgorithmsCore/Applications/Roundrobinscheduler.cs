namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ ОЧЕРЕДИ: планировщик процессов Round Robin (карусельный).
// Процесс берётся из начала очереди, работает не дольше кванта времени и, если не завершился,
// уходит в конец очереди. Так каждый процесс получает процессор по справедливому циклу (FIFO).
// Enqueue/Dequeue у нашей очереди O(1), всего шагов не больше суммы времён / квант + число процессов.
public class SimProcess
{
    public string Name { get; }
    public int Burst { get; }       // сколько всего нужно времени процессора
    public int Remaining { get; set; }
    public int FinishTime { get; set; }

    public SimProcess(string name, int burst)
    {
        Name = name;
        Burst = burst;
        Remaining = burst;
    }
}

public static class RoundRobinScheduler
{
    // Считаем, что все процессы пришли в момент времени 0
    public static void Run(SimProcess[] processes, int quantum)
    {
        if (quantum <= 0) throw new ArgumentException("Квант должен быть положительным", nameof(quantum));

        var queue = new DataStructures.MyQueue<SimProcess>();
        foreach (var p in processes) queue.Enqueue(p);

        int time = 0;
        Console.WriteLine($"Квант времени: {quantum}");

        while (!queue.IsEmpty())
        {
            var current = queue.Dequeue();
            int slice = Math.Min(quantum, current.Remaining);

            Console.WriteLine($"  t={time,3}..{time + slice,3}: {current.Name} (осталось {current.Remaining} -> {current.Remaining - slice})");

            time += slice;
            current.Remaining -= slice;

            if (current.Remaining > 0)
            {
                queue.Enqueue(current);      // не успел, встаёт в конец очереди
            }
            else
            {
                current.FinishTime = time;   // завершился
            }
        }

        Console.WriteLine("\nПроцесс | Время работы | Завершён | Ожидание");
        foreach (var p in processes)
        {
            // все пришли в 0: время в системе = момент завершения, ожидание = оно минус работа
            Console.WriteLine($"{p.Name,-7} | {p.Burst,12} | {p.FinishTime,8} | {p.FinishTime - p.Burst,8}");
        }
        Console.WriteLine($"Среднее ожидание: {processes.Average(p => p.FinishTime - p.Burst):F2}");
    }
}