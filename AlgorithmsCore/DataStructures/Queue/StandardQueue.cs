namespace AlgorithmsCore.DataStructures;

public class StandardQueue<T> : IQueue<T>
{
    private readonly Queue<T> _queue = new Queue<T>();

    public void Enqueue(T item) => _queue.Enqueue(item);
    public T Dequeue() => _queue.Dequeue();
    public T Top() => _queue.Peek();
    public bool IsEmpty() => _queue.Count == 0;
    
    public void Print()
    {
        Console.Write(string.Join(" ", _queue) + " ");
    }
}