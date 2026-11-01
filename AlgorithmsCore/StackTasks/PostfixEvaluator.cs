using System.Globalization;

namespace AlgorithmsCore.Tasks;

public class PostfixEvaluator
{
    public static double Evaluate(string expression)
    {
        var stack = new DataStructures.Stack<double>();
        var tokens = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            // Если это число — кладем в стек
            if (double.TryParse(token, CultureInfo.InvariantCulture, out double num))
            {
                stack.Push(num);
                continue;
            }

            // Унарные операции
            if (token is "sqrt" or "sin" or "cos" or "ln")
            {
                double a = stack.Pop();
                double result = token switch
                {
                    "sqrt" => Math.Sqrt(a),
                    "sin" => Math.Sin(a),
                    "cos" => Math.Cos(a),
                    "ln" => Math.Log(a),
                    _ => throw new ArgumentException("Неизвестная операция")
                };
                stack.Push(result);
            }
            // Бинарные операции
            else
            {
                double b = stack.Pop(); // Внимание: первым снимается правый операнд
                double a = stack.Pop();
                double result = token switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" => a / b,
                    "^" => Math.Pow(a, b),
                    _ => throw new ArgumentException("Неизвестная операция")
                };
                stack.Push(result);
            }
        }
        return stack.Pop();
    }
}