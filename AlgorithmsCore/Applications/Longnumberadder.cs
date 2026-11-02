namespace AlgorithmsCore.Applications;

// ПРИМЕНЕНИЕ СПИСКА: сложение длинных чисел.
// Цифры хранятся в односвязном списке в обратном порядке (младший разряд в голове),
// поэтому сложение «столбиком» идёт от головы к хвосту за один проход: O(max(n, m)).
// Число не ограничено размером int/long: память растёт по мере необходимости.
public static class LongNumberAdder
{
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
        var result = new DataStructures.LinkedList<int>();
        var x = a.head;
        var y = b.head;
        int carry = 0;

        while (x != null || y != null || carry != 0)
        {
            int sum = carry;
            if (x != null) { sum += x.data; x = x.next; }
            if (y != null) { sum += y.data; y = y.next; }

            result.AddLast(sum % 10);
            carry = sum / 10;
        }
        return result;
    }

    // Список 3 -> 2 -> 1 превращаем обратно в строку "123"
    public static string ToText(DataStructures.LinkedList<int> number)
    {
        var chars = number.Select(d => (char)('0' + d)).Reverse().ToArray();
        // убираем ведущие нули, но оставляем хотя бы один символ
        string text = new string(chars).TrimStart('0');
        return text.Length == 0 ? "0" : text;
    }

    public static string Add(string a, string b) =>
        ToText(Add(Parse(a), Parse(b)));
}