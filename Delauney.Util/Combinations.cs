using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Delauney.Util;
public static class Combinations
{
    private static void InitIndexes(int[] indexes)
    {
        for (int i = 0; i < indexes.Length; i++)
        {
            indexes[i] = i;
        }
    }

    private static void SetIndexes(int[] indexes, int lastIndex, int count)
    {
        indexes[lastIndex]++;
        if (lastIndex > 0 && indexes[lastIndex] == count)
        {
            SetIndexes(indexes, lastIndex - 1, count - 1);
            indexes[lastIndex] = indexes[lastIndex - 1] + 1;
        }
    }

    private static List<T> TakeAt<T>(int[] indexes, IEnumerable<T> list)
    {
        List<T> selected = new List<T>();
        for (int i = 0; i < indexes.Length; i++)
        {
            selected.Add(list.ElementAt(indexes[i]));
        }
        return selected;
    }

    private static bool AllPlacesChecked(int[] indexes, int places)
    {
        for (int i = indexes.Length - 1; i >= 0; i--)
        {
            if (indexes[i] != places)
                return false;
            places--;
        }
        return true;
    }
    public static IEnumerable<IEnumerable<T>> Cartesian<T>(this IEnumerable<T> col,int max)
    {
        if (max==1)
        {
            return Enumerable.Repeat(col,1);
        }
        else
        {
            return Cartesian(col.Join(col, x => x, y => y, (x, y) => Enumerable.Repeat(x,1).Append(y))
                    .SelectMany(x=>x),max-1);
        }
    }
    public static IEnumerable<List<T>> GetDifferentCombinations<T>(this IEnumerable<T> collection, int count)
    {
        int[] indexes = new int[count];
        int listCount = collection.Count();
        if (count > listCount)
            throw new InvalidOperationException($"{nameof(count)} is greater than the collection elements.");
        InitIndexes(indexes);
        do
        {
            var selected = TakeAt(indexes, collection);
            yield return selected;
            SetIndexes(indexes, indexes.Length - 1, listCount);
        }
        while (!AllPlacesChecked(indexes, listCount));

    }
}
