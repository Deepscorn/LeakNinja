using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LeakNinja
{
    internal class Node
    {
        internal readonly List<Node> Children = new List<Node>();
        internal readonly Watch Watch;
        internal readonly bool IsLeaking;

        internal Node(Watch watch, bool leaking)
        {
            Watch = watch;
            IsLeaking = leaking;
        }

        public override string ToString() => Watch == null ? "Root" : Watch.Name;
    }

    public class WatchSummaryFormatter
    {
        private int linesCount_ = 0;
        private int maxLinesCount_ = int.MaxValue;
        private readonly List<StringBuilder> builders_ = new List<StringBuilder>();
        private StringBuilder builder_ = new StringBuilder();
        public string Indent = "    ";

        public WatchSummaryFormatter() => builders_.Add(builder_);

        public string Format(IReadOnlyCollection<Watch> watches, string firstLine = null)
        {
            if (firstLine != null)
                AppendLine(firstLine);

            var root = BuildTree(watches);

            PrintSubtree(root, "");

            return builder_.ToString();
        }

        public string[] Format(IReadOnlyCollection<Watch> watches, int maxLines, string firstLine = null)
        {
            maxLinesCount_ = maxLines;

            if (firstLine != null)
                AppendLine(firstLine);

            var root = BuildTree(watches);

            PrintSubtree(root, "");

            return builders_.Select(b => b.ToString()).ToArray();
        }

        private static Node BuildTree(IReadOnlyCollection<Watch> watches)
        {
            // will build tree using dictionary for quick access

            // build dictionary
            var allNodes = new Dictionary<Watch, Node>();
            // first, add leaking nodes
            foreach (var watch in watches)
                allNodes.Add(watch, new Node(watch, true));

            // then add all nodes in-between to be able to build tree
            foreach (var watch in watches)
                AddMissingNodesUpToRoot(allNodes, watch);

            // now build tree
            var root = new Node(null, false);
            foreach (var node in allNodes.Values)
            {
                Node parent;
                switch (node.Watch)
                {
                    case GameWatch gameWatch:
                        parent = gameWatch.Parent == null ? root : allNodes[gameWatch.Parent];
                        break;
                    case ComponentWatch componentWatch:
                        parent = allNodes[componentWatch.GameWatch];
                        break;
                    default:
                        parent = root;
                        break;
                }
                parent.Children.Add(node);
            }

            // sort tree by names (local per each tree level)
            SortRecursive(root.Children);

            return root;
        }


        private static void AddMissingNodesUpToRoot(Dictionary<Watch, Node> allNodes, Watch watch)
        {
            while (true)
            {
                Watch dependency = null;
                switch (watch)
                {
                    case GameWatch gameWatch:
                        dependency = gameWatch.Parent;
                        break;
                    case ComponentWatch componentWatch:
                        dependency = componentWatch.GameWatch;
                        break;
                }

                if (dependency == null || allNodes.ContainsKey(dependency)) return;

                allNodes.Add(dependency, new Node(dependency, false));
                watch = dependency;
            }
        }

        private static void SortRecursive(List<Node> nodes)
        {
            nodes.Sort((a, b) => string.Compare(a.Watch.Name, b.Watch.Name, StringComparison.Ordinal));
            foreach (var node in nodes)
                SortRecursive(node.Children);
        }

        private void PrintSubtree(Node root, string shift)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < root.Children.Count;)
            {
                var node = root.Children[i];
                switch (node.Watch)
                {
                    case ComponentWatch _:
                        ++i;
                        continue;
                    case GameWatch _:
                        {
                            // print self
                            builder_.Append(shift);
                            builder_.Append(node.Watch.Name);

                            // go in-depth (flatten non-leaking game objects making path)
                            while (!node.IsLeaking && node.Children.Count == 1 && node.Children[0].Watch is GameWatch)
                            {
                                node = node.Children[0];
                                builder_.Append("/");
                                builder_.Append(node.Watch.Name);
                            }

                            // print components
                            builder_.Append(" (");
                            if (node.IsLeaking)
                                builder_.Append("GameObject");

                            foreach (var child in node.Children)
                            {
                                if (!(child.Watch is ComponentWatch))
                                    continue;
                                builder_.Append(' ');
                                builder_.Append(child.Watch.Name);
                            }

                            AppendLine(")");

                            PrintSubtree(node, shift + Indent);

                            ++i;
                            break;
                        }
                    default:
                        // group watches with same names, printing count
                        var count = 1;
                        for (i += 1; i < root.Children.Count; ++i)
                        {
                            var nextWatch = root.Children[i].Watch;
                            if (nextWatch.GetType() != typeof(ComponentWatch) && nextWatch.GetType() != typeof(GameWatch)
                                                                              && node.Watch.Name == nextWatch.Name)
                                ++count;
                            else
                                break;
                        }
                        builder_.Append(shift);
                        builder_.Append(node.Watch.Name);
                        if (count > 1)
                        {
                            builder_.Append(" (");
                            builder_.Append(count);
                            builder_.Append(')');
                        }

                        AppendLine();
                        break;
                }
            }
        }

        private void AppendLine(string line = null)
        {
            if (line == null)
                builder_.AppendLine();
            else
                builder_.AppendLine(line);
            ++linesCount_;
            if (linesCount_ < maxLinesCount_)
                return;

            linesCount_ = 0;
            builder_ = new StringBuilder();
            builders_.Add(builder_);
        }
    }
}