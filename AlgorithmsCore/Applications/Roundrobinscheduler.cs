namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ ОЧЕРЕДИ: планировщик процессов Round Robin (карусельный).
// Процесс берётся из начала очереди, работает не дольше кванта времени и, если не завершился,
// уходит в конец очереди. Так каждый процесс получает процессор по справедливому циклу (FIFO).
// Enqueue/Dequeue у нашей очереди O(1), всего шагов не больше суммы времён / квант + число процессов.
public class SimProcess
{
    public string Name { get; set; }
    public int Burst { get; set; }       // сколько всего нужно времени процессора
    public int Remaining { get; set; }
    public int FinishTime { get; set; }
    public int WaitingTime => FinishTime - Burst;

    public SimProcess(string name, int burst)
    {
        Name = name;
        Burst = burst;
        Remaining = burst;
    }

    public SimProcess Clone() => new SimProcess(Name, Burst) { Remaining = Burst };
}

public static class RoundRobinScheduler
{
    public record ExecutionSlice(
        int StartTime,
        int EndTime,
        string ProcessName,
        int SliceLength,
        int RemainingAfter,
        List<string> QueueAfter);

    public record SchedulerReport(
        List<ExecutionSlice> Slices,
        List<SimProcess> Processes,
        int TotalTime,
        double AverageWaitTime,
        double AverageTurnaroundTime);

    public static SchedulerReport Simulate(SimProcess[] processes, int quantum)
    {
        if (quantum <= 0) throw new ArgumentException("Квант должен быть положительным", nameof(quantum));

        var clonedProcesses = processes.Select(p => p.Clone()).ToArray();
        var queue = new DataStructures.MyQueue<SimProcess>();
        foreach (var p in clonedProcesses) queue.Enqueue(p);

        var slices = new List<ExecutionSlice>();
        int time = 0;

        while (!queue.IsEmpty())
        {
            var current = queue.Dequeue();
            int slice = Math.Min(quantum, current.Remaining);
            int startTime = time;
            time += slice;
            current.Remaining -= slice;

            if (current.Remaining > 0)
            {
                queue.Enqueue(current);
            }
            else
            {
                current.FinishTime = time;
            }

            var queueNames = queue.ToList().Select(p => $"{p.Name}({p.Remaining})").ToList();
            slices.Add(new ExecutionSlice(startTime, time, current.Name, slice, current.Remaining, queueNames));
        }

        double avgWait = clonedProcesses.Length > 0 ? clonedProcesses.Average(p => p.FinishTime - p.Burst) : 0;
        double avgTurnaround = clonedProcesses.Length > 0 ? clonedProcesses.Average(p => p.FinishTime) : 0;

        return new SchedulerReport(slices, clonedProcesses.ToList(), time, avgWait, avgTurnaround);
    }

    // Считаем, что все процессы пришли в момент времени 0
    public static void Run(SimProcess[] processes, int quantum)
    {
        var report = Simulate(processes, quantum);
        Console.WriteLine($"Квант времени: {quantum}");

        foreach (var slice in report.Slices)
        {
            Console.WriteLine($"  t={slice.StartTime,3}..{slice.EndTime,3}: {slice.ProcessName} (осталось {slice.RemainingAfter})");
        }

        Console.WriteLine("\nПроцесс | Время работы | Завершён | Ожидание");
        foreach (var p in report.Processes)
        {
            Console.WriteLine($"{p.Name,-7} | {p.Burst,12} | {p.FinishTime,8} | {p.WaitingTime,8}");
        }
        Console.WriteLine($"Среднее ожидание: {report.AverageWaitTime:F2}");
    }
}
