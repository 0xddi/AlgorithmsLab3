using System.Globalization;

namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ ДЕРЕВА: дерево выражений.
// Строится из постфиксной записи с помощью стека: число становится листом,
// оператор снимает поддеревья и становится их корнем.
// Вычисление и обходы рекурсивные, каждый узел посещается один раз: O(n).
public class ExpressionTree
{
    public class Node
    {
        public string Token { get; set; }
        public Node? Left { get; set; }
        public Node? Right { get; set; }

        public Node(string token, Node? left = null, Node? right = null)
        {
            Token = token;
            Left = left;
            Right = right;
        }

        public bool IsLeaf => Left == null && Right == null;
        public bool IsUnary => Left != null && Right == null;
    }

    private Node? root;

    public Node? Root => root;

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
            else if (token is "sqrt" or "sin" or "cos" or "ln")
            {
                if (stack.GetLength() < 1)
                    throw new FormatException($"Не хватает операндов для унарной операции '{token}'");

                var child = stack.Pop();
                stack.Push(new Node(token, child, null));
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
            throw new FormatException("Некорректное выражение: в стеке осталось более одного узла");

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

        if (node.IsUnary)
        {
            double a = Evaluate(node.Left!);
            return node.Token switch
            {
                "sqrt" => Math.Sqrt(a),
                "sin" => Math.Sin(a),
                "cos" => Math.Cos(a),
                "ln" => Math.Log(a),
                _ => throw new InvalidOperationException($"Неизвестная унарная операция '{node.Token}'")
            };
        }

        double leftVal = Evaluate(node.Left!);
        double rightVal = Evaluate(node.Right!);
        return node.Token switch
        {
            "+" => leftVal + rightVal,
            "-" => leftVal - rightVal,
            "*" => leftVal * rightVal,
            "/" or ":" => leftVal / rightVal,
            "^" => Math.Pow(leftVal, rightVal),
            _ => throw new InvalidOperationException($"Неизвестная операция '{node.Token}'")
        };
    }

    // Префиксный обход (корень, левое, правое)
    public string Prefix() => Join(root, (n, l, r) => n.IsUnary ? $"{n.Token} {l}" : $"{n.Token} {l} {r}");

    // Постфиксный обход (левое, правое, корень) возвращает исходную запись
    public string Postfix() => Join(root, (n, l, r) => n.IsUnary ? $"{l} {n.Token}" : $"{l} {r} {n.Token}");

    // Инфиксный обход со скобками (левое, корень, правое)
    public string Infix() => Join(root, (n, l, r) => n.IsUnary ? $"{n.Token}({l})" : $"({l} {n.Token} {r})");

    private static string Join(Node? node, Func<Node, string, string, string> format)
    {
        if (node == null) return "";
        if (node.IsLeaf) return node.Token;
        return format(node, Join(node.Left, format), Join(node.Right, format));
    }

    public int GetNodeCount() => CountNodes(root);
    public int GetDepth() => CalcDepth(root);

    private static int CountNodes(Node? n) => n == null ? 0 : 1 + CountNodes(n.Left) + CountNodes(n.Right);
    private static int CalcDepth(Node? n) => n == null ? 0 : 1 + Math.Max(CalcDepth(n.Left), CalcDepth(n.Right));
}
