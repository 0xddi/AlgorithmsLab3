using System.Globalization;

namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ ДЕРЕВА: дерево выражений.
// Строится из постфиксной записи с помощью стека: число становится листом,
// оператор снимает два поддерева и становится их корнем.
// Вычисление и обходы рекурсивные, каждый узел посещается один раз: O(n).
public class ExpressionTree
{
    private class Node
    {
        public string Token;
        public Node? Left;
        public Node? Right;

        public Node(string token, Node? left = null, Node? right = null)
        {
            Token = token;
            Left = left;
            Right = right;
        }

        public bool IsLeaf => Left == null && Right == null;
    }

    private Node? root;

    // Строим дерево из постфиксной записи, например "7 8 + 3 2 + :"
    public static ExpressionTree FromPostfix(string postfix)
    {
        var stack = new DataStructures.Stack<Node>();
        var tokens = postfix.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                stack.Push(new Node(token));
            }
            else if (token is "+" or "-" or "*" or "/" or ":" or "^")
            {
                if (stack.GetLength() < 2)
                    throw new FormatException($"Не хватает операндов для '{token}'");

                var right = stack.Pop(); // правый операнд снимается первым
                var left = stack.Pop();
                stack.Push(new Node(token, left, right));
            }
            else
            {
                throw new FormatException($"Неизвестный токен '{token}'");
            }
        }

        if (stack.GetLength() != 1)
            throw new FormatException("Некорректное выражение");

        return new ExpressionTree { root = stack.Pop() };
    }

    public double Evaluate()
    {
        if (root == null) throw new InvalidOperationException("Дерево пусто");
        return Evaluate(root);
    }

    private static double Evaluate(Node node)
    {
        if (node.IsLeaf) return double.Parse(node.Token, CultureInfo.InvariantCulture);

        double a = Evaluate(node.Left!);
        double b = Evaluate(node.Right!);
        return node.Token switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" or ":" => a / b,
            "^" => Math.Pow(a, b),
            _ => throw new InvalidOperationException("Неизвестная операция")
        };
    }

    // Префиксный обход (корень, левое, правое)
    public string Prefix() => Join(root, (n, l, r) => $"{n.Token} {l} {r}");

    // Постфиксный обход (левое, правое, корень) возвращает исходную запись
    public string Postfix() => Join(root, (n, l, r) => $"{l} {r} {n.Token}");

    // Инфиксный обход со скобками (левое, корень, правое)
    public string Infix() => Join(root, (n, l, r) => $"({l} {n.Token} {r})");

    private static string Join(Node? node, Func<Node, string, string, string> format)
    {
        if (node == null) return "";
        if (node.IsLeaf) return node.Token;
        return format(node, Join(node.Left, format), Join(node.Right, format));
    }
}