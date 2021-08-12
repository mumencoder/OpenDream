
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace OpenDreamShared.Compiler.DM {
    public static partial class DMAST {
        internal static StringBuilder PrintNode(DMASTNode n, int depth, int max_depth=-1) {
            StringBuilder sb = new();
            if (max_depth == 0) {
                return sb;
            }
            var pad = new String(' ', 2 * depth);
            if (n == null) {
                sb.Append("null");
                return sb;
            }
            sb.Append(pad + n.GetType().Name + ":");
            var new_max_depth = max_depth - 1;
            switch (n) {
                case DMASTIdentifier nn: sb.Append(nn.Identifier); break;
                case DMASTProcStatementLabel nn: sb.Append(nn.Name); break;
                case DMASTCallableProcIdentifier nn: sb.Append(nn.Identifier); break;
                case DMASTProcDefinition nn: sb.Append(nn.Name); break;
                case DMASTObjectDefinition nn: sb.Append(nn.Path); break;
                case DMASTProcCall nn: new_max_depth = -1; break;
                case DMASTDereferenceProc nn: {
                        new_max_depth = -1;
                        foreach (var def in nn.Dereferences) {
                            sb.Append(def.Property + ",");
                        }
                    }
                    new_max_depth = -1; break;
                case Testing.DMASTDereferenceIdentifier nn: new_max_depth = -1; break;
                default: break;
            }
            sb.Append('\n');
            foreach (var leaf in n.LeafNodes()) {
                sb.Append( PrintNode(leaf, depth + 1, new_max_depth) );
            }
            return sb;
        }
        public static string PrintNodes(this DMASTNode n, int max_depth = -1) {
            return PrintNode(n, 0, max_depth).ToString();
        }

        public delegate void CompareResult(DMASTNode n_l, DMASTNode n_r, string s);

        public static bool Compare(this DMASTNode node_l, DMASTNode node_r, CompareResult cr) {
            if (node_l == null || node_r == null) {
                if (node_r == node_l) { return true; }
                cr(node_l, node_r, "null mismatch");
                return false;
            }

            if (node_l.GetType() != node_r.GetType()) { cr(node_l, node_r, "type mismatch"); return false; }

            List<object> compared = new();
            DMASTNode[] subnodes_l = node_l.LeafNodes().ToArray();
            DMASTNode[] subnodes_r = node_r.LeafNodes().ToArray();

            if (subnodes_l.Length != subnodes_r.Length) { cr(node_l, node_r, "nodes length mismatch " + subnodes_l.Length + " " + subnodes_r.Length); return false; }

            for (var i = 0; i < subnodes_l.Length; i++) {
                Compare(subnodes_l[i], subnodes_r[i], cr);
                compared.Add(subnodes_l);
            }

            //foreach (var field in node_l.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            //    if (compared.Contains(field.GetValue(node_l))) {
            //        continue;
            //    }
            //}

            return true;
        }
        public static IEnumerable<DMASTNode> LeafNodes(this DMASTNode node) {
            foreach (var field in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)) {
                var value = field.GetValue(node);
                if (value == null) { continue; }
                if (field.FieldType.IsAssignableTo(typeof(DMASTNode))) {
                    yield return value as DMASTNode;
                }
                else if (field.FieldType.IsArray && field.FieldType.GetElementType().IsAssignableTo(typeof(DMASTNode))) {
                    var field_value = value as DMASTNode[];
                    foreach (var subnode in field_value) {
                        yield return subnode;
                    }
                }
            }
        }
    }
}
