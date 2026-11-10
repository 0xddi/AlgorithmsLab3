namespace AlgorithmsCore.DataStructures;

// Реализация стека (принцип LIFO) через односвязный список. Stack-пойнтер здесь всегда указывает
// на самое начало связного списка (т.е. стек всегда растёт в начале связного списка). 
// Сделано так для того, чтобы гарантировать O(1) сложность для операций push/pop
public class Stack<T>
{
    private readonly LinkedList<T> list = new LinkedList<T>();
    

    public bool IsEmpty()
    {
        return this.list.IsEmpty();
    }

    public void Push(T value)
    {
        this.list.AddFirst(value);
    }

    public T Pop()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Stack is empty");
        
        T value = this.list.GetFirst();
        this.list.RemoveFirst();
        return value;
    }

    public T Top()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Stack is empty");
        
        return this.list.GetFirst();
    }

    public void Print()
    {
        if (IsEmpty())
            throw new InvalidOperationException("Stack is empty");
        
        foreach (var item in this.list)
        {
            Console.Write(item);
            Console.Write(" ");
        }
    }

    public int GetLength()
    {
        return this.list.GetLength();
    }

    public List<T> ToList()
    {
        var result = new List<T>();
        foreach (var item in this.list)
        {
            result.Add(item);
        }
        return result;
    }
}
