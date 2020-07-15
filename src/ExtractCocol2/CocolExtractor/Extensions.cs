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

    public static D[] Add<S, D>(this S[] arr, D element) where S : D
    {
        var newArr = new D[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[^1] = element;
        return newArr;
    }

    public static D[] AddRange<S, D>(this S[] arr, ICollection<D> elements) where S : D
    {
        var newArr = new D[arr.Length + elements.Count];
        Array.Copy(arr, newArr, arr.Length);
        Array.Copy(elements.ToArray(), 0, newArr, arr.Length, elements.Count);
        return newArr;
    }

    public static D[] Add<S, D>(this D[] arr, S element) where S : D
    {
        var newArr = new D[arr.Length + 1];
        Array.Copy(arr, newArr, arr.Length);
        newArr[^1] = element;
        return newArr;
    }

    public static D[] AddRange<S, D>(this D[] arr, ICollection<S> elements) where S : D
    {
        var newArr = new D[arr.Length + elements.Count];
        Array.Copy(arr, newArr, arr.Length);
        Array.Copy(elements.ToArray(), 0, newArr, arr.Length, elements.Count);
        return newArr;
    }
}
