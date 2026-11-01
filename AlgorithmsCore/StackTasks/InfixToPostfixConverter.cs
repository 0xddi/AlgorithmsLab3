using System.Globalization;

namespace AlgorithmsCore.Tasks;

public class InfixToPostfixConverter
{
    private static int GetPriority(string op)
    {
        return op switch
        {
            "+" or "-" => 1,
            "*" or "/" => 2,
            "^" => 3,
            "(" => 0,
            _ => -1
        };
    }

    public static string Convert(string infix)
    {
        var output = new List<string>();
        var operators = new DataStructures.Stack<string>();
        
        // Добавляем пробелы вокруг скобок для корректного сплита
        string formatted = infix.Replace("(", "( ").Replace(")", " )");
        var tokens = formatted.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (double.TryParse(token, CultureInfo.InvariantCulture, out _))
            {
                output.Add(token); // Число сразу в выход
            }
            else if (token == "(")
            {
                operators.Push(token);
            }
            else if (token == ")")
            {
                while (!operators.IsEmpty() && operators.Top() != "(")
                {
                    output.Add(operators.Pop());
                }
                operators.Pop(); // Убираем "(" из стека
            }
            else // Оператор (+, -, *, /, ^)
            {
                // Извлекаем операторы с большим или равным приоритетом
                while (!operators.IsEmpty() && GetPriority(operators.Top()) >= GetPriority(token))
                {
                    output.Add(operators.Pop());
                }
                operators.Push(token);
            }
        }

        // Вытаскиваем оставшиеся операторы
        while (!operators.IsEmpty())
        {
            output.Add(operators.Pop());
        }

        return string.Join(" ", output);
    }
}