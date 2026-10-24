namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleLogicalOrdering
{
    // Packet insertion and rollback mutate this private index; range walks do not allocate snapshots.
    private sealed class PacketRangeIndex
    {
        public sealed class Node((int Offset, int Index) key, Node? parent)
        {
            public (int Offset, int Index) Key = key;
            public Node? Parent = parent;
            public Node? Left;
            public Node? Right;
            public int Height = 1;
        }

        private Node? root;

        public Node? FirstAtOrAfter((int Offset, int Index) key)
        {
            Node? current = root;
            Node? first = null;
            while (current is not null)
            {
                if (current.Key.CompareTo(key) >= 0)
                {
                    first = current;
                    current = current.Left;
                }
                else
                {
                    current = current.Right;
                }
            }
            return first;
        }

        public Node? Next(Node current)
        {
            if (current.Right is { } right)
            {
                current = right;
                while (current.Left is { } left)
                {
                    current = left;
                }
                return current;
            }
            while (current.Parent is { } parent)
            {
                if (ReferenceEquals(parent.Left, current))
                {
                    return parent;
                }
                current = parent;
            }
            return null;
        }

        public bool Add((int Offset, int Index) key)
        {
            Node? current = root;
            Node? parent = null;
            int comparison = 0;
            while (current is not null)
            {
                comparison = key.CompareTo(current.Key);
                if (comparison == 0)
                {
                    return false;
                }
                parent = current;
                current = comparison < 0 ? current.Left : current.Right;
            }
            Node added = new(key, parent);
            if (parent is null)
            {
                root = added;
            }
            else if (comparison < 0)
            {
                parent.Left = added;
            }
            else
            {
                parent.Right = added;
            }
            BalanceFrom(parent);
            return true;
        }

        public bool Remove((int Offset, int Index) key)
        {
            Node? current = FirstAtOrAfter(key);
            if (current is null || current.Key != key)
            {
                return false;
            }
            if (current.Left is not null && current.Right is not null)
            {
                Node successor = Next(current)!;
                current.Key = successor.Key;
                current = successor;
            }
            Node? parent = current.Parent;
            Replace(current, current.Left ?? current.Right);
            BalanceFrom(parent);
            return true;
        }

        private void BalanceFrom(Node? current)
        {
            while (current is not null)
            {
                UpdateHeight(current);
                int balance = Height(current.Left) - Height(current.Right);
                if (balance > 1)
                {
                    Node left = current.Left!;
                    if (Height(left.Left) < Height(left.Right))
                    {
                        RotateLeft(left);
                    }
                    current = RotateRight(current);
                }
                else if (balance < -1)
                {
                    Node right = current.Right!;
                    if (Height(right.Right) < Height(right.Left))
                    {
                        RotateRight(right);
                    }
                    current = RotateLeft(current);
                }
                current = current.Parent;
            }
        }

        private Node RotateLeft(Node current)
        {
            Node next = current.Right!;
            current.Right = next.Left;
            if (current.Right is { } moved)
            {
                moved.Parent = current;
            }
            Replace(current, next);
            next.Left = current;
            current.Parent = next;
            UpdateHeight(current);
            UpdateHeight(next);
            return next;
        }

        private Node RotateRight(Node current)
        {
            Node next = current.Left!;
            current.Left = next.Right;
            if (current.Left is { } moved)
            {
                moved.Parent = current;
            }
            Replace(current, next);
            next.Right = current;
            current.Parent = next;
            UpdateHeight(current);
            UpdateHeight(next);
            return next;
        }

        private void Replace(Node current, Node? replacement)
        {
            Node? parent = current.Parent;
            if (parent is null)
            {
                root = replacement;
            }
            else if (ReferenceEquals(parent.Left, current))
            {
                parent.Left = replacement;
            }
            else
            {
                parent.Right = replacement;
            }
            if (replacement is not null)
            {
                replacement.Parent = parent;
            }
        }

        private static int Height(Node? node) => node?.Height ?? 0;

        private static void UpdateHeight(Node node)
        {
            node.Height = Math.Max(Height(node.Left), Height(node.Right)) + 1;
        }
    }
}
