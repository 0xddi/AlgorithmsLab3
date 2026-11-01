namespace AlgorithmsCore.DataStructures;

// Реализация обычного бинарного дерева. 
// Добавление элементов и десериализация выполняются
// через префиксный обход (pre-order)
public class BinaryTree<T>
{
    private class Node
    {
        public Node left;
        public Node right;
        public T value;

        public Node(T value)
        {
            this.value = value;
        }
    }

    private Node root;

    public bool IsEmpty()
    {
        if (root == null) return true;
        return false;
    }

    // Принимаем функцию парсинга (как из char получить T)
    public void BuildFromString(string data, Func<char, T> parser)
    {
        int index = 0;
        root = BuildNode(data, ref index, parser);
    }

    private Node BuildNode(string data, ref int index, Func<char, T> parser)
    {
        if (index >= data.Length) return null;

        char symbol = data[index];
        index++;

        if (symbol == '*') return null;

        
        T val = parser(symbol); 
        
        Node node = new Node(val);

        node.left = BuildNode(data, ref index, parser);
        node.right = BuildNode(data, ref index, parser);

        return node;
    }

    public void PrintInOrder()
    {
        PrintInOrder(root);
    }

    private void PrintInOrder(Node node)
    {
        if (node == null) return;
        
        PrintInOrder(node.left);
        Console.Write(node.value + " ");
        PrintInOrder(node.right);
    }
}