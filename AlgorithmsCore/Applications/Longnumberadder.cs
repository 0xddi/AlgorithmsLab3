namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ СПИСКА: сложение длинных чисел.
// Цифры хранятся в односвязном списке в обратном порядке (младший разряд в голове),
// поэтому сложение «столбиком» идёт от головы к хвосту за один проход: O(max(n, m)).
// Число не ограничено размером int/long: память растёт по мере необходимости.
public static class LongNumberAdder
{
    public record AdditionStep(
        int ColumnIndex,
        int DigitA,
        int DigitB,
        int InputCarry,
        int Sum,
        int ResultDigit,
        int OutputCarry);

    // "123" -> список 3 -> 2 -> 1
    public static DataStructures.LinkedList<int> Parse(string number)
    {
        if (string.IsNullOrEmpty(number) || !number.All(char.IsDigit))
            throw new ArgumentException("Число должно состоять только из цифр", nameof(number));

        var list = new DataStructures.LinkedList<int>();
        foreach (char c in number)
        {
            list.AddFirst(c - '0'); // каждая следующая цифра становится новой головой
        }
        return list;
    }

    public static DataStructures.LinkedList<int> Add(
        DataStructures.LinkedList<int> a, DataStructures.LinkedList<int> b)
    {
        var (result, _) = AddWithTraceInternal(a, b);
        return result;
    }

    public static (string Result, List<AdditionStep> Steps) AddWithSteps(string a, string b)
    {
        var listA = Parse(a);
        var listB = Parse(b);
        var (resultList, steps) = AddWithTraceInternal(listA, listB);
        return (ToText(resultList), steps);
    }

    private static (DataStructures.LinkedList<int> Result, List<AdditionStep> Steps) AddWithTraceInternal(
        DataStructures.LinkedList<int> a, DataStructures.LinkedList<int> b)
    {
        var result = new DataStructures.LinkedList<int>();
        var steps = new List<AdditionStep>();
        var x = a.head;
        var y = b.head;
        int carry = 0;
        int col = 0;

        while (x != null || y != null || carry != 0)
        {
            int digA = x != null ? x.data : 0;
            int digB = y != null ? y.data : 0;
            int inCarry = carry;

            int sum = inCarry + digA + digB;
            int resDig = sum % 10;
            int outCarry = sum / 10;

            result.AddLast(resDig);
            carry = outCarry;

            steps.Add(new AdditionStep(col, digA, digB, inCarry, sum, resDig, outCarry));
            col++;

            if (x != null) x = x.next;
            if (y != null) y = y.next;
        }

        return (result, steps);
    }

    // Список 3 -> 2 -> 1 превращаем обратно в строку "123"
    public static string ToText(DataStructures.LinkedList<int> number)
    {
        var chars = number.Select(d => (char)('0' + d)).Reverse().ToArray();
        string text = new string(chars).TrimStart('0');
        return text.Length == 0 ? "0" : text;
    }

    public static string Add(string a, string b) =>
        ToText(Add(Parse(a), Parse(b)));
}
