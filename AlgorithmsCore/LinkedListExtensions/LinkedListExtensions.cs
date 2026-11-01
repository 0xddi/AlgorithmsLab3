namespace AlgorithmsCore.LinkedListExtensions;

using System;
using System.Collections.Generic;


public static class LinkedListExtensions
{
    // 1. Написать функцию, которая переворачивает список L, т.е. изменяет ссылки в этом
    // списке так, чтобы его элементы оказались расположенными в обратном порядке.
    public static void Reverse<T>(this DataStructures.LinkedList<T> list)
    {
        if (list.IsEmpty() || list.head == list.tail) return;

        DataStructures.LinkedList<T>.Node prev = null;
        var current = list.head;
        
        // Старая голова станет новым хвостом
        list.tail = list.head;

        while (current != null)
        {
            var nextTemp = current.next;
            current.next = prev;
            prev = current;
            current = nextTemp;
        }

        list.head = prev;
    }

    // 2. Написать функцию, которая переносит в начало (в конец) непустого списка L его
    // последний (первый) элемент.
    public static void MoveLastToFirst<T>(this DataStructures.LinkedList<T> list)
    {
        if (list.IsEmpty() || list.head == list.tail) return;

        var current = list.head;
        // Ищем предпоследний элемент
        while (current.next != list.tail)
        {
            current = current.next;
        }

        var oldTail = list.tail;
        current.next = null;      // Отрезаем хвост
        list.tail = current;      // Новый хвост - предпоследний
        oldTail.next = list.head; // Старый хвост указывает на старую голову
        list.head = oldTail;      // Старый хвост становится новой головой
    }

    public static void MoveFirstToLast<T>(this DataStructures.LinkedList<T> list)
    {
        if (list.IsEmpty() || list.head == list.tail) return;

        var oldHead = list.head;
        list.head = oldHead.next; // Голова сдвигается
        oldHead.next = null;      // Старая голова отрезается
        list.tail.next = oldHead; // Хвост указывает на старую голову
        list.tail = oldHead;      // Старая голова становится новым хвостом
    }

    // 3. Написать функцию, которая определяет количество различных элементов списка,
    // содержащего целые числа.
    public static int CountDistinct<T>(this DataStructures.LinkedList<T> list)
    {
        var uniqueSet = new HashSet<T>();
        var current = list.head;
        while (current != null)
        {
            uniqueSet.Add(current.data);
            current = current.next;
        }
        return uniqueSet.Count;
    }

    // 4. Написать функцию, которая удаляет из списка L неуникальные элементы.
    public static void RemoveNonUnique<T>(this DataStructures.LinkedList<T> list)
    {
        var counts = new Dictionary<T, int>();
        var current = list.head;
        
        // Считаем частоту
        while (current != null)
        {
            counts[current.data] = counts.TryGetValue(current.data, out int count) ? count + 1 : 1;
            current = current.next;
        }

        // Удаляем дубликаты
        while (list.head != null && counts[list.head.data] > 1)
        {
            list.RemoveFirst();
        }

        if (list.head == null) return;

        current = list.head;
        while (current.next != null)
        {
            if (counts[current.next.data] > 1)
            {
                current.next = current.next.next; // Перепрыгиваем
                if (current.next == null) list.tail = current; // Если удалили хвост
            }
            else
            {
                current = current.next;
            }
        }
    }

    // 5. Написать функцию вставки списка самого в себя вслед за первым вхождением числа x.
    public static void InsertSelfAfter<T>(this DataStructures.LinkedList<T> list, T x)
    {
        var current = list.head;
        
        // Ищем первое вхождение x
        while (current != null && !EqualityComparer<T>.Default.Equals(current.data, x))
        {
            current = current.next;
        }

        if (current == null) return; // Не нашли

        // Делаем копию элементов списка для вставки
        var elementsToInsert = new List<T>(list);
        
        // Вставляем копию после current
        var nextNode = current.next;
        var insertTail = current;

        foreach (var item in elementsToInsert)
        {
            insertTail.next = new DataStructures.LinkedList<T>.Node(item);
            insertTail = insertTail.next;
        }

        insertTail.next = nextNode; // Соединяем конец вставки с остатком списка

        if (current == list.tail) // Если вставляли после последнего элемента
        {
            list.tail = insertTail;
        }
    }

    // 6. Написать функцию, которая вставляет в непустой список L, элементы которого
    // упорядочены по не убыванию, новый элемент E так, чтобы сохранилась упорядоченность.
    public static void InsertSorted<T>(this DataStructures.LinkedList<T> list, T e) where T : IComparable<T>
    {
        var newNode = new DataStructures.LinkedList<T>.Node(e);

        // Если список пуст или элемент меньше головы
        if (list.IsEmpty() || e.CompareTo(list.head.data) <= 0)
        {
            list.AddFirst(e);
            return;
        }

        var current = list.head;
        // Ищем место для вставки
        while (current.next != null && current.next.data.CompareTo(e) < 0)
        {
            current = current.next;
        }

        // Вставляем
        newNode.next = current.next;
        current.next = newNode;

        // Если вставили в конец, обновляем хвост
        if (current == list.tail)
        {
            list.tail = newNode;
        }
    }

    // 7. Написать функцию, которая удаляет из списка L все элементы E, если таковые имеются.
    public static void RemoveAll<T>(this DataStructures.LinkedList<T> list, T e)
    {
        // Чистим голову
        while (list.head != null && EqualityComparer<T>.Default.Equals(list.head.data, e))
        {
            list.RemoveFirst();
        }

        if (list.head == null) return;

        var current = list.head;
        while (current.next != null)
        {
            if (EqualityComparer<T>.Default.Equals(current.next.data, e))
            {
                current.next = current.next.next;
                if (current.next == null) list.tail = current; // Обновили хвост
            }
            else
            {
                current = current.next;
            }
        }
    }

    // 8. Написать функцию, которая вставляет в список L новый элемент F перед первым
    // вхождением элемента E, если E входит в L.
    public static void InsertBefore<T>(this DataStructures.LinkedList<T> list, T e, T f)
    {
        if (list.IsEmpty()) return;

        // Если E — это голова
        if (EqualityComparer<T>.Default.Equals(list.head.data, e))
        {
            list.AddFirst(f);
            return;
        }

        var current = list.head;
        while (current.next != null)
        {
            if (EqualityComparer<T>.Default.Equals(current.next.data, e))
            {
                var newNode = new DataStructures.LinkedList<T>.Node(f, current.next);
                current.next = newNode;
                return; // Вставили, выходим
            }
            current = current.next;
        }
    }

    // 9. Функция дописывает к списку L список E. Оба списка содержат целые числа. В
    // основной программе считать их из файла.
    public static void Append<T>(this DataStructures.LinkedList<T> list, DataStructures.LinkedList<T> other)
    {
        var current = other.head;
        while (current != null)
        {
            list.AddLast(current.data);
            current = current.next;
        }
    }

    // 10. Функция разбивает список целых чисел на два списка по первому вхождению
    // заданного числа. Если этого числа в списке нет, второй список будет пустым, а
    // первый не изменится.
    public static (DataStructures.LinkedList<T> First, DataStructures.LinkedList<T> Second) SplitByFirstOccurrence<T>(this DataStructures.LinkedList<T> list, T target)
    {
        var first = new DataStructures.LinkedList<T>();
        var second = new DataStructures.LinkedList<T>();
        
        var current = list.head;
        bool found = false;

        while (current != null)
        {
            if (!found && EqualityComparer<T>.Default.Equals(current.data, target))
            {
                found = true;
            }

            if (found)
                second.AddLast(current.data);
            else
                first.AddLast(current.data);

            current = current.next;
        }

        return (first, second);
    }

    // 11. Функция удваивает список, т.е. приписывает в конец списка себя самого.
    public static void Double<T>(this DataStructures.LinkedList<T> list)
    {
        if (list.IsEmpty()) return;

        var elementsCopy = new List<T>(list); // Сохраняем копию, чтобы не уйти в бесконечность
        foreach (var item in elementsCopy)
        {
            list.AddLast(item);
        }
    }

    // 12. Функция меняет местами два элемента списка, заданные пользователем
    public static void Swap<T>(this DataStructures.LinkedList<T> list, int index1, int index2)
    {
        int length = list.GetLength();
        if (index1 < 0 || index2 < 0 || index1 >= length || index2 >= length || index1 == index2) 
            return;

        // Проще всего найти узлы и поменять их данные (data)
        var node1 = list.head;
        for (int i = 0; i < index1; i++) node1 = node1.next;

        var node2 = list.head;
        for (int i = 0; i < index2; i++) node2 = node2.next;

        (node1.data, node2.data) = (node2.data, node1.data);
    }
}