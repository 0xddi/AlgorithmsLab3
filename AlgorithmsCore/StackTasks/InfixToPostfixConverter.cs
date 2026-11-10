using System.Globalization;
using System.Text;

namespace AlgorithmsCore.Tasks;

public class InfixToPostfixConverter
{
    public record ConversionStep(int StepIndex, string Token, string Action, List<string> StackState, List<string> OutputTokens);

    private static int GetPriority(string op)
    {
        return op switch
        {
            "+" or "-" => 1,
            "*" or "/" or ":" => 2,
            "^" => 3,
            "sqrt" or "sin" or "cos" or "ln" => 4,
            "(" => 0,
            _ => -1
        };
    }

    public static string[] Tokenize(string infix)
    {
        // Вставляем пробелы вокруг скобок и операторов
        var sb = new StringBuilder();
        for (int i = 0; i < infix.Length; i++)
        {
            char c = infix[i];
            if (c is '(' or ')' or '+' or '*' or '/' or ':' or '^')
            {
                sb.Append(' ').Append(c).Append(' ');
            }
            else if (c == '-')
            {
                // Проверяем, унарный ли это минус или бинарный
                bool isUnary = sb.ToString().TrimEnd().EndsWith("(") || sb.ToString().TrimEnd().Length == 0;
                if (isUnary && i + 1 < infix.Length && (char.IsDigit(infix[i + 1]) || infix[i + 1] == '.'))
                {
                    sb.Append(c); // часть отрицательного числа
                }
                else
                {
                    sb.Append(' ').Append(c).Append(' ');
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    public static string Convert(string infix)
    {
        var (postfix, _) = ConvertWithTrace(infix);
        return postfix;
    }

    public static (string Postfix, List<ConversionStep> Steps) ConvertWithTrace(string infix)
    {
        var output = new List<string>();
        var operators = new DataStructures.Stack<string>();
        var steps = new List<ConversionStep>();
        var tokens = Tokenize(infix);
        int stepIdx = 0;

        foreach (var token in tokens)
        {
            stepIdx++;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                output.Add(token);
                steps.Add(new ConversionStep(stepIdx, token, $"Число {token} отправлено в выходной поток", operators.ToList(), new List<string>(output)));
            }
            else if (token is "sqrt" or "sin" or "cos" or "ln")
            {
                operators.Push(token);
                steps.Add(new ConversionStep(stepIdx, token, $"Функция '{token}' помещена в стек операторов", operators.ToList(), new List<string>(output)));
            }
            else if (token == "(")
            {
                operators.Push(token);
                steps.Add(new ConversionStep(stepIdx, token, "Открывающая скобка '(' помещена в стек", operators.ToList(), new List<string>(output)));
            }
            else if (token == ")")
            {
                while (!operators.IsEmpty() && operators.Top() != "(")
                {
                    string op = operators.Pop();
                    output.Add(op);
                }
                if (!operators.IsEmpty() && operators.Top() == "(")
                {
                    operators.Pop(); // удаляем '('
                }

                // Если перед скобкой была функция (например sin(...)), переносим её в вывод
                if (!operators.IsEmpty() && operators.Top() is "sqrt" or "sin" or "cos" or "ln")
                {
                    string func = operators.Pop();
                    output.Add(func);
                }

                steps.Add(new ConversionStep(stepIdx, token, "Закрывающая скобка ')' вытолкнула операторы из стека", operators.ToList(), new List<string>(output)));
            }
            else // Оператор (+, -, *, /, :, ^)
            {
                while (!operators.IsEmpty() && GetPriority(operators.Top()) >= GetPriority(token) && token != "^")
                {
                    output.Add(operators.Pop());
                }
                operators.Push(token);
                steps.Add(new ConversionStep(stepIdx, token, $"Оператор '{token}' помещен в стек (приоритет {GetPriority(token)})", operators.ToList(), new List<string>(output)));
            }
        }

        while (!operators.IsEmpty())
        {
            stepIdx++;
            string op = operators.Pop();
            if (op != "(" && op != ")")
            {
                output.Add(op);
            }
            steps.Add(new ConversionStep(stepIdx, "EOF", $"Остаток из стека '{op}' перемещен в вывод", operators.ToList(), new List<string>(output)));
        }

        string result = string.Join(" ", output);
        return (result, steps);
    }
}
