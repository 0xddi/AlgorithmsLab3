using System.Globalization;

namespace AlgorithmsCore.Tasks;

public class PostfixEvaluator
{
    public record EvalStep(int StepIndex, string Token, string Action, double? ProducedValue, List<double> StackState);

    public static double Evaluate(string expression)
    {
        var (result, _) = EvaluateWithTrace(expression);
        return result;
    }

    public static (double Result, List<EvalStep> Steps) EvaluateWithTrace(string expression)
    {
        var stack = new DataStructures.Stack<double>();
        var steps = new List<EvalStep>();
        var tokens = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int stepIdx = 0;

        foreach (var token in tokens)
        {
            stepIdx++;
            // Если это число — кладем в стек
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                stack.Push(num);
                steps.Add(new EvalStep(stepIdx, token, $"Поместить число {num} в стек", num, stack.ToList()));
                continue;
            }

            // Унарные операции
            if (token is "sqrt" or "sin" or "cos" or "ln")
            {
                if (stack.IsEmpty())
                    throw new InvalidOperationException($"Недостаточно аргументов для операции '{token}'");

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
                steps.Add(new EvalStep(stepIdx, token, $"Вычислить {token}({a:G6}) = {result:G6}", result, stack.ToList()));
            }
            // Бинарные операции (+, -, *, /, :, ^)
            else if (token is "+" or "-" or "*" or "/" or ":" or "^")
            {
                if (stack.GetLength() < 2)
                    throw new InvalidOperationException($"Недостаточно аргументов для операции '{token}'");

                double b = stack.Pop(); // правый операнд
                double a = stack.Pop(); // левый операнд
                double result = token switch
                {
                    "+" => a + b,
                    "-" => a - b,
                    "*" => a * b,
                    "/" or ":" => a / b,
                    "^" => Math.Pow(a, b),
                    _ => throw new ArgumentException("Неизвестная операция")
                };
                stack.Push(result);
                steps.Add(new EvalStep(stepIdx, token, $"Вычислить {a:G6} {token} {b:G6} = {result:G6}", result, stack.ToList()));
            }
            else
            {
                throw new ArgumentException($"Неизвестный символ: '{token}'");
            }
        }

        if (stack.GetLength() != 1)
            throw new InvalidOperationException("Некорректное постфиксное выражение: в стеке осталось более одного элемента или стек пуст");

        double finalResult = stack.Pop();
        return (finalResult, steps);
    }
}
