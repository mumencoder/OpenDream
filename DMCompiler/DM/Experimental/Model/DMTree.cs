
using System.Collections.Generic;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class DMTree {
        Dictionary<string, DMTree> Children = new();

        List<IDMSymbol> AssociatedSymbols;

        public DMTree GetTree(DreamPath path) {
            DMTree tree = this;
            foreach (var segment in path.Elements) {
                tree = tree.GetTree(segment);
            }
            return tree;
        }

        public DMTree GetTree(string segment) {
            if (!Children.ContainsKey(segment)) {
                Children[segment] = new DMTree();
            }
            return Children[segment];
        }

    }
}
