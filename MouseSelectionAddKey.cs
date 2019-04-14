namespace MultiCaret
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Input;

    public class MouseSelectionAddKey
    {
        private static readonly IDictionary<int, MouseSelectionAddKey> KeysById;

        static MouseSelectionAddKey()
        {
            AllKeys = typeof(MouseSelectionAddKey)
                .GetProperties(BindingFlags.Static | BindingFlags.Public)
                .Where(o => o.PropertyType == typeof(MouseSelectionAddKey))
                .Select(o => (MouseSelectionAddKey)o.GetValue(null))
                .Distinct()
                .ToList();

            KeysById = AllKeys.ToDictionary(o => o.ID);
        }

        private MouseSelectionAddKey(int id, string name, IList<Key> keys)
        {
            this.ID = id;
            this.Name = name;
            this.Keys = keys;
        }

        public static IList<MouseSelectionAddKey> AllKeys { get; }

        public static MouseSelectionAddKey Alt { get; } =
            new MouseSelectionAddKey(1, "Alt", new[] { Key.LeftAlt, Key.RightAlt });

        public static MouseSelectionAddKey Ctrl { get; } =
            new MouseSelectionAddKey(2, "Ctrl", new[] { Key.LeftCtrl, Key.RightCtrl });

        public static MouseSelectionAddKey Default => Alt;

        public int ID { get; }

        public string Name { get; }

        public IList<Key> Keys { get; }

        public static MouseSelectionAddKey GetById(int id)
        {
            return KeysById.TryGetValue(id, out var key) ? key : null;
        }
    }
}
