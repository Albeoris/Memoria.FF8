namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleLogicalOrdering
{
    // Packet insertion and rollback mutate this private index; slot handles never leave a range walk.
    private sealed class PacketRangeIndex
    {
        public const int NoSlot = -1;

        private struct Node((int Offset, int Index) key, int parent)
        {
            public (int Offset, int Index) Key = key;
            public int Parent = parent;
            public int Left = NoSlot;
            public int Right = NoSlot;
            public int Height = 1;
        }

        private readonly OrderingPages<Node> nodes = new();
        private int root = NoSlot;
        private int freeHead = NoSlot;

        public (int Offset, int Index) ReadKey(int slot) => nodes.Slot(slot).Key;

        public int FirstAtOrAfter((int Offset, int Index) key)
        {
            int current = root;
            int first = NoSlot;
            while (current != NoSlot)
            {
                ref readonly Node node = ref nodes.Slot(current);
                if (node.Key.CompareTo(key) >= 0)
                {
                    first = current;
                    current = node.Left;
                }
                else
                {
                    current = node.Right;
                }
            }
            return first;
        }

        public int Next(int current)
        {
            int right = nodes.Slot(current).Right;
            if (right != NoSlot)
            {
                current = right;
                for (int left = nodes.Slot(current).Left; left != NoSlot; left = nodes.Slot(current).Left)
                {
                    current = left;
                }
                return current;
            }
            int parent = nodes.Slot(current).Parent;
            while (parent != NoSlot)
            {
                ref readonly Node ancestor = ref nodes.Slot(parent);
                if (ancestor.Left == current)
                {
                    return parent;
                }
                current = parent;
                parent = ancestor.Parent;
            }
            return NoSlot;
        }

        public bool Add((int Offset, int Index) key)
        {
            int current = root;
            int parent = NoSlot;
            int comparison = 0;
            while (current != NoSlot)
            {
                ref readonly Node node = ref nodes.Slot(current);
                comparison = key.CompareTo(node.Key);
                if (comparison == 0)
                {
                    return false;
                }
                parent = current;
                current = comparison < 0 ? node.Left : node.Right;
            }
            int added = Allocate(new(key, parent));
            if (parent == NoSlot)
            {
                root = added;
            }
            else if (comparison < 0)
            {
                nodes.Slot(parent).Left = added;
            }
            else
            {
                nodes.Slot(parent).Right = added;
            }
            BalanceFrom(parent);
            return true;
        }

        public bool Remove((int Offset, int Index) key)
        {
            int current = FirstAtOrAfter(key);
            if (current == NoSlot || ReadKey(current) != key)
            {
                return false;
            }
            ref Node node = ref nodes.Slot(current);
            if (node.Left != NoSlot && node.Right != NoSlot)
            {
                int successor = Next(current);
                node.Key = ReadKey(successor);
                current = successor;
                node = ref nodes.Slot(current);
            }
            int parent = node.Parent;
            Replace(current, node.Left != NoSlot ? node.Left : node.Right);
            // A released row reuses its parent field for the free list until the next allocation.
            node.Parent = freeHead;
            freeHead = current;
            BalanceFrom(parent);
            return true;
        }

        private int Allocate(in Node node)
        {
            if (freeHead == NoSlot)
            {
                int appended = nodes.Count;
                nodes.Add(node);
                return appended;
            }
            int reused = freeHead;
            ref Node slot = ref nodes.Slot(reused);
            freeHead = slot.Parent;
            slot = node;
            return reused;
        }

        private void BalanceFrom(int current)
        {
            while (current != NoSlot)
            {
                UpdateHeight(current);
                ref readonly Node node = ref nodes.Slot(current);
                int balance = Height(node.Left) - Height(node.Right);
                if (balance > 1)
                {
                    int left = node.Left;
                    ref readonly Node child = ref nodes.Slot(left);
                    if (Height(child.Left) < Height(child.Right))
                    {
                        RotateLeft(left);
                    }
                    current = RotateRight(current);
                }
                else if (balance < -1)
                {
                    int right = node.Right;
                    ref readonly Node child = ref nodes.Slot(right);
                    if (Height(child.Right) < Height(child.Left))
                    {
                        RotateRight(right);
                    }
                    current = RotateLeft(current);
                }
                current = nodes.Slot(current).Parent;
            }
        }

        private int RotateLeft(int current)
        {
            ref Node node = ref nodes.Slot(current);
            int next = node.Right;
            ref Node replacement = ref nodes.Slot(next);
            node.Right = replacement.Left;
            if (node.Right != NoSlot)
            {
                nodes.Slot(node.Right).Parent = current;
            }
            Replace(current, next);
            replacement.Left = current;
            node.Parent = next;
            UpdateHeight(current);
            UpdateHeight(next);
            return next;
        }

        private int RotateRight(int current)
        {
            ref Node node = ref nodes.Slot(current);
            int next = node.Left;
            ref Node replacement = ref nodes.Slot(next);
            node.Left = replacement.Right;
            if (node.Left != NoSlot)
            {
                nodes.Slot(node.Left).Parent = current;
            }
            Replace(current, next);
            replacement.Right = current;
            node.Parent = next;
            UpdateHeight(current);
            UpdateHeight(next);
            return next;
        }

        private void Replace(int current, int replacement)
        {
            int parent = nodes.Slot(current).Parent;
            if (parent == NoSlot)
            {
                root = replacement;
            }
            else
            {
                ref Node ancestor = ref nodes.Slot(parent);
                if (ancestor.Left == current)
                {
                    ancestor.Left = replacement;
                }
                else
                {
                    ancestor.Right = replacement;
                }
            }
            if (replacement != NoSlot)
            {
                nodes.Slot(replacement).Parent = parent;
            }
        }

        private int Height(int slot) => slot == NoSlot ? 0 : nodes.Slot(slot).Height;

        private void UpdateHeight(int slot)
        {
            ref Node node = ref nodes.Slot(slot);
            node.Height = Math.Max(Height(node.Left), Height(node.Right)) + 1;
        }
    }
}
