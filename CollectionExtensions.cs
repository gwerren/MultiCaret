namespace MultiCaret
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    public static class CollectionExtensions
    {
        public static ObservableCollection<T> ToObservableCollection<T>(this IEnumerable<T> values)
        {
            return new ObservableCollection<T>(values);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> values)
        {
            return new HashSet<T>(values);
        }
    }
}
