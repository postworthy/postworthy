using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Tasks.Update.Models
{
    public static class ExtensionMethods
    {
        private static IEnumerable<TResult> SelectWithPrevious<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, int, TResult> projection)
        {
            using (var iterator = source.GetEnumerator())
            {
                int index = 0;
                if (!iterator.MoveNext())
                {
                    yield break;
                }
                TSource previous = iterator.Current;
                while (iterator.MoveNext())
                {
                    yield return projection(previous, iterator.Current, index++);
                    previous = iterator.Current;
                }
            }
        }

        public static bool IsWithinAverageRecurrenceInterval(this IEnumerable<DateTime> dates, DateTime date = default(DateTime), int multiplier = 1)
        {
            if (dates.Count() > 0)
            {
                if (date == default(DateTime))
                    date = DateTime.Now.ToUniversalTime();
                else
                    date = date.ToUniversalTime();

                var ordered = dates.Select(x => x.ToUniversalTime()).OrderBy(x => x);
                var current = date - ordered.First();

                if (dates.Count() > 2)
                {
                    var average = TimeSpan.FromSeconds(multiplier * ordered.SelectWithPrevious((prev, cur, index) => { return index > 0 ? (cur - prev).Seconds : int.MinValue; }).Where(x => x != int.MinValue).Average());
                    return current < average;
                }
                else if (dates.Count() == 2)
                    return current < TimeSpan.FromSeconds(Math.Abs((dates.First() - dates.Skip(1).First()).TotalSeconds) * multiplier);
            }
            return true;
        }
    }
}
