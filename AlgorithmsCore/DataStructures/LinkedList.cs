using System.Collections;

namespace AlgorithmsCore.DataStructures;

// Реалиазация односвязного списка
public class LinkedList<T> : IEnumerable<T>
{
    public Node head;
    public Node tail; 

    public class Node
    {
        public Node next;
        public T data;

        public Node(T data)
        {
            this.data = data;
            this.next = null;
        }

        public Node(T data, Node next)
        {
            this.data = data;
            this.next = next;
        }
    }

    public void AddLast(T data)
    {
        Node newNode = new Node(data);
        
        if (this.head == null)
        {
            this.head = newNode;
            this.tail = newNode;
            return;
        }
        
        this.tail.next = newNode;
        this.tail = newNode;
    }

    public void AddFirst(T data)
    {
        Node newNode = new Node(data, this.head);
        this.head = newNode;
        
        if (this.tail == null)
        {
            this.tail = newNode;
        }
    }

    public bool IsEmpty()
    {
        return this.head == null;
    }

    public void RemoveFirst()
    {
        if (IsEmpty()) throw new InvalidOperationException("The list is empty");
        
        this.head = this.head.next;

        // Если мы удалили последний элемент, хвост тоже должен обнулиться
        if (this.head == null)
        {
            this.tail = null;
        }
    }

    public void RemoveLast()
    {
        if (IsEmpty()) throw new InvalidOperationException("The list is empty");
        
        // Случай, если один узел
        if (this.head.next == null)
        {
            this.head = null;
            this.tail = null; 
            return;
        }
  
        Node tempNode = this.head;
        while (tempNode.next != this.tail)
        {
            tempNode = tempNode.next;
        }
        
        tempNode.next = null;
        this.tail = tempNode; 
    }

    public T GetAtIndex(int index)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));

        Node tempNode = this.head;
        while (tempNode != null && index != 0)
        {
            tempNode = tempNode.next;
            index--;
        }
        
        if (tempNode == null) throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");

        return tempNode.data;
    }

    public T GetFirst()
    {
        if (IsEmpty()) throw new InvalidOperationException("The list is empty");
        return this.head.data;
    }
    
    public T GetLast()
    {
        if (IsEmpty()) throw new InvalidOperationException("The list is empty");
        
        return this.tail.data; 
    }

    public int GetLength()
    {
        int length = 0;
        var tempNode = this.head;

        while (tempNode != null)
        {
            length++;
            tempNode = tempNode.next;
        }

        return length;
    }
    
    public IEnumerator<T> GetEnumerator()
    {
        Node currentNode = this.head;
        while (currentNode != null)
        {
            yield return currentNode.data;
            currentNode = currentNode.next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}