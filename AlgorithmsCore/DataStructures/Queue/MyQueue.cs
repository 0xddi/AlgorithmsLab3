namespace AlgorithmsCore.DataStructures;

// Реализация очереди (принцип FIFO) через односвязный список
public interface IQueue<T>
{
    void Enqueue(T item);
    T Dequeue();
    T Top();
    bool IsEmpty();
    void Print();
    List<T> ToList();
}

public class MyQueue<T> : IQueue<T>
{
    private readonly LinkedList<T> list = new LinkedList<T>();

    // Проверка на пустоту
    public bool IsEmpty()
    {
        return this.list.IsEmpty();
    }
    
    // Добавление элемента в очередь
    public void Enqueue(T item)
    {
        list.AddLast(item);
    }

    public T Dequeue()
    {
        if (IsEmpty()) throw new InvalidOperationException("MyQueue is empty");
        var value = this.list.GetFirst();
        list.RemoveFirst();
        return value;
    }

    // Вывод первого элемента
    public T Top()
    {
        if (IsEmpty()) throw new InvalidOperationException("MyQueue is empty");

        return this.list.GetFirst();
    }
    
    // Вывод всех значений
    public void Print()
    {
        foreach (var item in list)
        {
            Console.Write(item);
            Console.Write(" ");
        }
    }

    public List<T> ToList()
    {
        var result = new List<T>();
        foreach (var item in list)
        {
            result.Add(item);
        }
        return result;
    }
}
