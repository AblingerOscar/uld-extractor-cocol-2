using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static T[] Add<T>(this T[] arr, T element)
    {
        var newArr = new T[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[^1] = element;
        return newArr;
    }

    public static T[] AddRange<T>(this T[] arr, ICollection<T> elements)
    {
        var newArr = new T[arr.Length + elements.Count];
        Array.Copy(arr, newArr, arr.Length);
        Array.Copy(elements.ToArray(), 0, newArr, arr.Length, elements.Count);
        return newArr;
    }

    public static R[] Add<T1, T2, R>(this T1[] arr, T2 element)
        where T1 : R
        where T2 : R
    {
        var newArr = new R[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[^1] = element;
        return newArr;
    }

    public static R[] AddRange<T1, T2, R>(this T1[] arr, ICollection<T2> elements)
        where T1 : R
        where T2 : R
    {
        var newArr = new R[arr.Length + elements.Count];
        Array.Copy(arr, newArr, arr.Length);
        Array.Copy(elements.ToArray(), 0, newArr, arr.Length, elements.Count);
        return newArr;
    }
}
